# FEAT-35: Confirmação de cadastro via código (OTP)

## Objetivo

Permitir que um usuário recém-registrado confirme o próprio cadastro
digitando o código de 6 dígitos que o Cognito já envia por email no
`SignUp` (`POST /auth/register`), hoje sem nenhuma forma de ser
confirmado pela API — e reenviar esse código quando necessário.

## Contexto

Item do backlog (`backend/docs/backlog.md`), parte da leva de features
de auth iniciada com o protótipo de telas de `frontend/design-system/`
(FEAT-33/34). Depende de FEAT-01 (auth), já pronta. Recomendado depois
de FEAT-34 (Custom Message trigger) pra já nascer com o email de marca
própria, mas não é bloqueio técnico — funciona igual com o email padrão
do Cognito.

Hoje, depois de `POST /auth/register`, o usuário fica com status
`UNCONFIRMED` no Cognito e não existe nenhum endpoint que chame
`ConfirmSignUp`/`ResendConfirmationCode` — a única forma de confirmar é
manualmente (console AWS ou CLI). `POST /auth/login` já rejeita login
de usuário não confirmado (`AuthErrors.UserNotConfirmed`, 401,
`user-not-confirmed`) desde antes desta feature — esse comportamento
não muda.

**Decisão já confirmada com o usuário (backlog, 2026-09-01):** o timer
de 60s do protótipo (`otpSeconds`) é só um cooldown de reenvio no
frontend, não uma expiração real de código — o backend usa o TTL nativo
do Cognito para o código (`ConfirmSignUp`/`ResendConfirmationCode`),
sem tabela própria de OTP nem mecanismo de rate limit/brute force
adicional.

**Decisões fechadas com o usuário durante o `/specify`:**

1. **Email inexistente ou código incorreto em `POST /auth/confirm`
   recebem o mesmo erro** (`invalid-confirmation-code`, 400) — evita
   que a API revele se um email está cadastrado ou não (mesmo
   princípio de não-enumeração já seguido implicitamente pelo restante
   da API de auth).
2. **Confirmar um usuário que já está confirmado é idempotente**: a
   API retorna 200, não erro — cobre o caso de o usuário reenviar o
   formulário (ex.: duplo clique, aba duplicada) sem quebrar a UX do
   fluxo de OTP.
3. **`POST /auth/resend-confirmation` sempre retorna 200**, mesmo
   quando o email não existe ou já está confirmado — mesmo princípio
   de não-enumeração da decisão 1, aplicado ao reenvio (o Cognito é
   chamado, mas seu resultado não é exposto ao cliente como erro).
4. **Nenhum dos dois endpoints retorna corpo em caso de sucesso** (200
   vazio) — mesmo padrão já usado por `POST /auth/logout`. O frontend
   já sabe o email que enviou; não há dado adicional a devolver.

## Requisitos de negócio

- `POST /auth/confirm` recebe `email` e `code`; chama o equivalente a
  `ConfirmSignUpAsync` do Cognito
- `POST /auth/resend-confirmation` recebe `email`; chama o equivalente
  a `ResendConfirmationCodeAsync` do Cognito
- `email` e `code`/`email` são obrigatórios em cada endpoint
  respectivamente — ausência de qualquer um retorna 400
  (`validation-error`, mesmo padrão do `ValidationBehavior` já usado
  pelas demais rotas, ex.: `POST /auth/register`)
- `POST /auth/confirm` com código incorreto (`CodeMismatchException`)
  ou email não encontrado no Cognito (`UserNotFoundException`) retorna
  400 (`invalid-confirmation-code`) — mesma resposta para os dois casos
  (decisão 1)
- `POST /auth/confirm` com código expirado (`ExpiredCodeException`)
  retorna 400 (`expired-confirmation-code`)
- `POST /auth/confirm` para um usuário já confirmado (Cognito recusa
  com `NotAuthorizedException`, "Current status is CONFIRMED") retorna
  200, sem erro (decisão 2)
- `POST /auth/resend-confirmation` sempre retorna 200, independentemente
  de o email existir, já estar confirmado, ou o Cognito recusar o
  reenvio (decisão 3) — exceções não mapeadas explicitamente (ex.:
  `UserNotFoundException`, `InvalidParameterException` de "already
  confirmed") são absorvidas e tratadas como sucesso; qualquer exceção
  verdadeiramente inesperada do SDK continua caindo no
  `GlobalExceptionHandler` (500), igual ao resto da API
- Nenhuma mudança no TTL/política de expiração do código no Cognito —
  continua o padrão nativo do User Pool, sem tabela própria de OTP
- `POST /auth/login` continua exatamente como hoje: usuário não
  confirmado recebe 401 (`user-not-confirmed`); esta feature não altera
  esse comportamento, apenas dá ao usuário um caminho para confirmar
  o cadastro e então logar normalmente

## User Stories

**US1 — Confirmação com código correto**
- Given um usuário registrado via `POST /auth/register`, ainda não
  confirmado, que recebeu o código de 6 dígitos por email
- When ele chama `POST /auth/confirm` com `email` e o `code` correto
- Then a API retorna 200 (sem corpo), e o usuário passa a poder fazer
  `POST /auth/login` normalmente

**US2 — Código incorreto**
- Given um usuário não confirmado
- When ele chama `POST /auth/confirm` com um `code` que não confere
- Then a API retorna 400 (`invalid-confirmation-code`), o usuário
  continua não confirmado

**US3 — Código expirado**
- Given um usuário não confirmado cujo código já passou do TTL do
  Cognito
- When ele chama `POST /auth/confirm` com esse código
- Then a API retorna 400 (`expired-confirmation-code`)

**US4 — Email inexistente**
- Given um `email` que nunca foi registrado
- When alguém chama `POST /auth/confirm` com esse `email` e qualquer
  `code`
- Then a API retorna 400 (`invalid-confirmation-code`) — mesma resposta
  da US2, sem revelar que o email não existe

**US5 — Confirmar duas vezes é idempotente**
- Given um usuário já confirmado (ex.: já chamou `POST /auth/confirm`
  com sucesso antes)
- When ele chama `POST /auth/confirm` novamente (com qualquer `code`)
- Then a API retorna 200, sem erro

**US6 — Reenvio de código**
- Given um usuário não confirmado
- When ele chama `POST /auth/resend-confirmation` com o `email`
- Then a API retorna 200 (sem corpo), e um novo código de 6 dígitos é
  enviado por email pelo Cognito

**US7 — Reenvio para email inexistente ou já confirmado não revela nada**
- Given um `email` que não existe, ou que já está confirmado
- When alguém chama `POST /auth/resend-confirmation` com esse `email`
- Then a API retorna 200 (sem corpo) igualmente, sem indicar se o email
  existe ou já foi confirmado

## Contratos da API

### POST /auth/confirm

Request:
```json
{
  "email": "neto@email.com",
  "code": "123456"
}
```

Response 200: sem corpo.

Response 400 (parâmetro ausente):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de confirmação é obrigatório."
}
```

Response 400 (código incorreto ou email inexistente):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-confirmation-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de confirmação inválido."
}
```

Response 400 (código expirado):
```json
{
  "type": "https://gastosapp.dev/errors/expired-confirmation-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de confirmação expirado."
}
```

### POST /auth/resend-confirmation

Request:
```json
{
  "email": "neto@email.com"
}
```

Response 200: sem corpo (sempre, ver decisão 3 — inclusive quando o
email não existe ou já está confirmado).

Response 400 (parâmetro ausente):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Email é obrigatório."
}
```

### Erros comuns

Formato padrão de erro do projeto
(`GastosApp.Api/Common/ResultHttpExtensions.cs`): `title` fixo e
genérico por tipo de erro (RFC 9457), mensagem específica sempre em
`detail`. Fonte de verdade exata: `backend/docs/openapi.json`.

## Critérios de aceite

- [ ] `POST /auth/confirm` com email e código corretos retorna 200 (sem
      corpo), e o usuário passa a logar normalmente via `POST /auth/login`
      (US1)
- [ ] `POST /auth/confirm` com código incorreto retorna 400
      (`invalid-confirmation-code`) (US2)
- [ ] `POST /auth/confirm` com código expirado retorna 400
      (`expired-confirmation-code`) (US3)
- [ ] `POST /auth/confirm` com email inexistente retorna 400
      (`invalid-confirmation-code`), mesma resposta de código incorreto
      (US4)
- [ ] `POST /auth/confirm` para usuário já confirmado retorna 200, sem
      erro (US5)
- [ ] `POST /auth/resend-confirmation` com email de usuário não
      confirmado retorna 200 e dispara um novo código pelo Cognito (US6)
- [ ] `POST /auth/resend-confirmation` com email inexistente ou já
      confirmado retorna 200 igualmente, sem revelar a diferença (US7)
- [ ] Ausência de `email`/`code` em qualquer um dos dois endpoints
      retorna 400 (`validation-error`)
- [ ] `POST /auth/login` continua retornando 401
      (`user-not-confirmed`) para usuário não confirmado, sem mudança
      de comportamento
- [ ] Nenhuma mudança no TTL/política de expiração de código do
      Cognito User Pool (nem em `backend/infra/terraform/`)
- [ ] Os dois novos endpoints cobertos por teste de componente (mock
      de `IAuthService`/Cognito)
- [ ] Os dois novos endpoints cobertos por teste integrado (pelo menos
      o fluxo de sucesso), rodado localmente via
      `backend/infra/lambda/run-local.sh` antes de a feature ser dada
      por concluída
- [ ] Suíte completa de testes (unitário + componente) passando
- [ ] `backend/docs/openapi.json` regenerado refletindo os dois novos
      endpoints

## Fora do escopo

- Qualquer mudança no TTL/expiração real do código no Cognito — o
  cooldown de 60s do frontend (FEAT-31, `frontend/specs/`) continua
  sendo só uma UX de reenvio, não uma garantia do backend
- Rate limiting/brute-force próprio para tentativas de confirmação —
  fica a cargo das proteções nativas do Cognito
- Recuperação de senha (`POST /auth/forgot-password`,
  `POST /auth/reset-password`) — escopo da FEAT-36
- Qualquer mudança no template HTML dos emails de confirmação/reenvio
  (`01-confirmacao-cadastro.html`) — já resolvido na FEAT-34
- Qualquer mudança de contrato em `POST /auth/register`,
  `POST /auth/login`, `GET /auth/me`, `POST /auth/refresh` ou
  `POST /auth/logout`
