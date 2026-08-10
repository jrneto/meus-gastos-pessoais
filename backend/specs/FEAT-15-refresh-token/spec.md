# FEAT-15: Refresh Token

## Objetivo
Permitir que um usuário autenticado permaneça logado ao atualizar a página
(F5) ou reabrir a aba, sem ser redirecionado para o login antes do
`accessToken` expirar de fato. Hoje o `accessToken` (IdToken do Cognito)
expira em 1h e não há como renová-lo — o usuário precisa refazer login.

Esta feature introduz um `refreshToken` emitido pelo Cognito no login,
transportado em **cookie httpOnly + Secure**, e um endpoint `POST
/auth/refresh` que troca esse refresh token por um novo `accessToken` sem
exigir senha novamente. Também introduz `POST /auth/logout` para encerrar
a sessão limpando o cookie.

Escopo desta feature é **exclusivamente backend**. O consumo pelo frontend
(retry automático em 401, chamada a `/auth/refresh` na inicialização da
app) fica para uma feature futura do contexto `/frontend`.

## Requisitos de negócio

- O `refreshToken` do Cognito nunca é exposto no corpo (body) de nenhuma
  resposta — é setado pelo backend via header `Set-Cookie`, sempre
  `HttpOnly`, `Secure` e `SameSite=Strict`, com `Path=/auth` (restrito às
  rotas de autenticação) e `Max-Age` alinhado à validade do refresh token
  no Cognito (5 dias, já configurado em `FEAT-09`)
- `POST /auth/login` passa a, além do corpo já existente, setar esse
  cookie com o `refreshToken` retornado pelo `InitiateAuth`
  (`USER_PASSWORD_AUTH`) do Cognito
- `POST /auth/refresh` lê o `refreshToken` do cookie da requisição (nunca
  do body) e troca por um novo `accessToken` via `InitiateAuth`
  (`REFRESH_TOKEN_AUTH`) do Cognito
- Sem rotação de refresh token: o mesmo `refreshToken` é reutilizável em
  chamadas sucessivas a `/auth/refresh` até expirar (comportamento padrão
  do fluxo `REFRESH_TOKEN_AUTH` do Cognito) ou até logout
- `userId` do usuário associado ao refresh também nunca vem do body —
  extraído das claims do novo `accessToken` emitido pelo Cognito
- `POST /auth/logout` limpa o cookie do refresh token (`Set-Cookie` com
  `Max-Age=0`). Não é necessário chamar revogação no Cognito nesta
  feature — a limpeza do cookie já impede o cliente de renovar a sessão;
  revogação server-side explícita (`RevokeToken`/`GlobalSignOut`) fica
  fora do escopo
- Requisição a `/auth/refresh` ou `/auth/logout` sem o cookie de refresh
  token presente retorna 401 (não é erro de validação de body)
- Requisição a `/auth/refresh` com refresh token expirado ou inválido
  (revogado, malformado) retorna 401 e o cookie é limpo pelo backend na
  própria resposta de erro, evitando retries automáticos infinitos no
  cliente com um cookie morto

## User stories

### US1 — Renovar accessToken sem novo login
**Given** um usuário fez login e possui um cookie de refresh token válido
**When** ele chama `POST /auth/refresh` (ex.: ao atualizar a página e
detectar que o `accessToken` em memória expirou)
**Then** recebe 200 com um novo `accessToken` válido, sem precisar
reenviar email/senha

### US2 — Refresh token ausente
**Given** um usuário sem cookie de refresh token (nunca logou, ou já
expirou/foi limpo)
**When** ele chama `POST /auth/refresh`
**Then** recebe 401, sinalizando que precisa refazer login

### US3 — Refresh token expirado ou inválido
**Given** um usuário com cookie de refresh token presente mas expirado
(mais de 5 dias) ou inválido
**When** ele chama `POST /auth/refresh`
**Then** recebe 401 e o cookie é removido na resposta

### US4 — Login emite refresh token
**Given** um usuário com credenciais válidas
**When** ele chama `POST /auth/login`
**Then** recebe 200 com o corpo já existente (`accessToken`, `expiresIn`,
`userId`) **e** um cookie `refreshToken` httpOnly/Secure na resposta

### US5 — Logout encerra a sessão renovável
**Given** um usuário logado com cookie de refresh token válido
**When** ele chama `POST /auth/logout`
**Then** recebe 200 e o cookie de refresh token é removido; chamadas
subsequentes a `POST /auth/refresh` passam a retornar 401 (US2)

## Contratos da API

### POST /auth/login (contrato existente, response ampliada)
Request: *(inalterado)*
```json
{
  "email": "neto@email.com",
  "password": "Senha123"
}
```

Response 200 *(corpo inalterado; passa a incluir cookie)*:
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "userId": "uuid-do-cognito"
}
```
Header adicional: `Set-Cookie: refreshToken=<token>; HttpOnly; Secure; SameSite=Strict; Path=/auth; Max-Age=432000`

Response 401 (credenciais inválidas): *(inalterado)*

### POST /auth/refresh (novo)
Sem request body. Requer o cookie `refreshToken` (enviado automaticamente
pelo navegador nas requisições para `/auth/*`).

Response 200:
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "userId": "uuid-do-cognito"
}
```

Response 401 (cookie ausente):
```json
{
  "type": "https://gastosapp.dev/errors/refresh-token-missing",
  "title": "Refresh token ausente.",
  "status": 401
}
```
Header adicional: `Set-Cookie: refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/auth; Expires=Thu, 01 Jan 1970 00:00:00 GMT`

Response 401 (refresh token expirado ou inválido — cookie é limpo nesta resposta):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-refresh-token",
  "title": "Refresh token inválido ou expirado.",
  "status": 401
}
```
Header adicional: `Set-Cookie: refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/auth; Expires=Thu, 01 Jan 1970 00:00:00 GMT`

Observação: como os demais erros de negócio do projeto (ex.: `invalid-credentials`
na FEAT-01), o `type` usa o código específico do erro (`error.Code`), e a
mensagem vai em `title` — `detail` só é preenchido para `ErrorType.Validation`
(ver `ResultHttpExtensions.BuildProblem`). O `type` genérico
`.../unauthorized` é usado só pelo middleware de JWT (`GET /auth/me` sem
token) e pelo `GlobalExceptionHandler`, não pelos erros de negócio do Mediator.

### POST /auth/logout (novo)
Sem request body. Cookie `refreshToken` opcional (se ausente, ainda assim
retorna 200 — logout é idempotente).

Response 200: sem corpo.
Header: `Set-Cookie: refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/auth; Expires=Thu, 01 Jan 1970 00:00:00 GMT`

## Plano de testes
- Componente: `POST /auth/login` seta o cookie `refreshToken` na resposta
  (além do corpo já coberto por `FEAT-01`)
- Componente: `POST /auth/refresh` com cookie válido retorna 200 com novo
  `accessToken`
- Componente: `POST /auth/refresh` sem cookie retorna 401
- Componente: `POST /auth/refresh` com cookie de refresh token
  inválido/expirado (mock do Cognito retornando erro) retorna 401 e limpa
  o cookie
- Componente: `POST /auth/logout` limpa o cookie e retorna 200, com e sem
  cookie presente na requisição
- Unitário: handler de refresh mapeia corretamente erro do Cognito
  (`NotAuthorizedException`) para `ErrorType.Unauthorized`

## Critérios de aceite
- [x] `POST /auth/login` continua retornando o corpo já existente e passa
      a setar o cookie httpOnly/Secure `refreshToken`
- [x] `POST /auth/refresh` com cookie válido retorna 200 com novo
      `accessToken`, sem exigir email/senha
- [x] `POST /auth/refresh` sem cookie retorna 401
- [x] `POST /auth/refresh` com cookie expirado/inválido retorna 401 e
      limpa o cookie na resposta
- [x] `POST /auth/logout` limpa o cookie e retorna 200 (idempotente, com
      ou sem cookie presente)
- [x] Refresh token nunca aparece no corpo de nenhuma resposta ou log
- [x] Todos os erros seguem RFC 9457 (ProblemDetails)
- [x] Testes de componente cobrem os cenários do plano de testes acima
- [x] `backend/docs/openapi.json` atualizado refletindo os novos
      endpoints e o novo header `Set-Cookie` em `/auth/login`,
      `/auth/refresh` e `/auth/logout`

## Fora do escopo desta FEAT
- Consumo pelo frontend (retry automático em 401, chamada a
  `/auth/refresh` no boot da app) — feature futura em `/frontend`
- Rotação de refresh token a cada uso
- Revogação server-side explícita no Cognito (`RevokeToken`/
  `GlobalSignOut`) — logout hoje só limpa o cookie local
- MFA, recuperação de senha (já fora do escopo desde `FEAT-01`)