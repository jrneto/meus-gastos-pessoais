# FEAT-36: Recuperação de senha (esqueci minha senha)

## Objetivo

Permitir que um usuário que esqueceu a senha a redefina sozinho, sem
depender de suporte manual: pedir um código de 6 dígitos por email,
informar esse código junto com uma senha nova, e receber a confirmação
de que a troca deu certo — inclusive um email de aviso de segurança
assim que a senha muda.

## Contexto

Item do backlog (`backend/docs/backlog.md`), parte da leva de features
de auth iniciada com o protótipo de telas de `frontend/design-system/`
(FEAT-33/34/35). Depende só de FEAT-33 (infraestrutura SES) — não
depende de FEAT-34 (Custom Message trigger) nem de FEAT-35 (confirmação
de cadastro) para funcionar, embora reaproveite exatamente o mesmo
padrão de erro/contrato que a FEAT-35 já estabeleceu para OTP.

Hoje não existe nenhuma forma de um usuário recuperar o acesso à
própria conta além de pedir a alguém com acesso ao console AWS para
trocar a senha manualmente no Cognito.

**O que já está pronto e esta feature reaproveita sem precisar de
código novo:**
- `CustomMessage_ForgotPassword` (FEAT-34) já troca o corpo padrão do
  email de "esqueci minha senha" do Cognito pelo HTML de
  `frontend/design-system/emails/02-recuperacao-senha.html`,
  substituindo `{{codigo}}` pelo código gerado — isso já acontece
  automaticamente assim que o backend chamar `ForgotPassword` no
  Cognito, sem nenhuma mudança nesta feature.
- IAM `ses:SendEmail`/`ses:SendRawEmail` para a Lambda da API principal
  já foi concedido na FEAT-33 (`lambda.tf` de hom/prod), escopado à
  identidade de domínio do próprio ambiente — suficiente para o envio
  direto do email de "senha alterada" desta feature, sem IAM novo.
  Único Terraform novo necessário: 2 parâmetros `String` no Parameter
  Store (`Ses/SenderEmail`, um por ambiente, custo zero e sem impacto
  de segurança) pra expor ao app o remetente já calculado pelo
  `email_configuration` do Cognito — decisão tomada e confirmada com o
  usuário durante o `/plan` (ver `plan.md`).
- As chamadas do Cognito usadas aqui (`ForgotPassword`,
  `ConfirmForgotPassword`) fazem parte da lista de operações que **não
  exigem policy IAM própria** quando chamadas só com `ClientId` (mesma
  categoria de `SignUp`/`ConfirmSignUp`/`ResendConfirmationCode`, já
  usadas hoje sem entrada dedicada em `lambda.tf`) — sem mudança de
  Terraform também por esse lado.

**Decisão já confirmada com o usuário (backlog, 2026-09-01):** o timer
de 60s do protótipo (`otpSeconds`) é só um cooldown de reenvio no
frontend, não uma expiração real de código — o backend usa o TTL nativo
do Cognito para o código (`ForgotPassword`/`ConfirmForgotPassword`), sem
tabela própria de OTP nem mecanismo de rate limit/brute force adicional
(retomado e confirmado de novo abaixo, decisão 3).

**Decisões fechadas com o usuário durante o `/specify`:**

1. **`POST /auth/forgot-password` sempre retorna 200**, mesmo quando o
   email não existe ou pertence a um usuário ainda não confirmado —
   mesmo princípio de não-enumeração já usado por
   `POST /auth/resend-confirmation` (FEAT-35, decisão 3). O
   `prevent_user_existence_errors = "ENABLED"` do User Pool
   (`cognito.tf`, hom e prod) já garante isso para email inexistente a
   nível de `ForgotPassword` em si; o backend reforça o mesmo princípio
   também para o caso de `InvalidParameterException` (usuário ainda não
   confirmado, sem atributo de contato verificado).
2. **Código incorreto e email inexistente em `POST /auth/reset-password`
   nunca revelam a diferença** (sempre 400, mesmo princípio de
   não-enumeração da FEAT-35, decisão 1) — **redação original previa o
   mesmo `type` (`invalid-reset-code`) para os dois casos; corrigido em
   2026-09-02** (ver "Status", achado na primeira execução real contra
   hom): contra o Cognito real, com `prevent_user_existence_errors=
   "ENABLED"` (`cognito.tf`), email inexistente devolve
   `expired-reset-code`, não `invalid-reset-code` — mesma anti-
   enumeração da FEAT-35 (`ExpiredCodeException` em vez de
   `UserNotFoundException`). O princípio continua valendo, só o `type`
   específico difere do que se assumia originalmente.
3. **Sem mecanismo próprio de rate limit/brute-force** para o código de
   reset — mesma decisão já tomada para a FEAT-35 (confirmação de
   cadastro), agora estendida deliberadamente também à recuperação de
   senha. Contexto: existe pesquisa de segurança pública (Pentagrid,
   2021) sobre o throttling nativo do Cognito ter sido, na prática, mais
   fraco que o anunciado para este fluxo específico — vulnerabilidade
   corrigida pela AWS em abril/2021, sem recorrência pública conhecida
   desde então. Avaliado durante este `/specify`: aceitar a proteção
   nativa do Cognito como suficiente hoje, sem introduzir throttling
   próprio nesta feature (mais escopo/complexidade). Reavaliar se algum
   dia houver evidência de abuso real.
4. **Email de "senha alterada" (`03-senha-alterada.html`) sem
   personalização por nome**: o template do protótipo tem um trecho
   "Olá, {{nome}}." — mas `POST /auth/reset-password` não é autenticado
   (não há JWT/sessão), então o backend não tem o nome do usuário à mão
   sem uma chamada nova (`AdminGetUser` do Cognito, que exige permissão
   IAM própria — recurso novo a aprovar). Decisão: ajustar o texto do
   template para não depender de `{{nome}}` (troca a saudação por algo
   como "A senha da conta {{email}} foi redefinida com sucesso.", já
   presente no corpo do email) em vez de adicionar a chamada/permissão
   nova só para personalizar.
5. **Nenhum dos dois endpoints retorna corpo em caso de sucesso** (200
   vazio) — mesmo padrão já usado por `POST /auth/logout` e pelos dois
   endpoints da FEAT-35.

## Requisitos de negócio

- `POST /auth/forgot-password` recebe `email`; chama o equivalente a
  `ForgotPasswordAsync` do Cognito, disparando o código de 6 dígitos
  por email (via `CustomMessage_ForgotPassword` + SES, já resolvido pela
  FEAT-33/34)
- `POST /auth/reset-password` recebe `email`, `code` e `newPassword`;
  chama o equivalente a `ConfirmForgotPasswordAsync` do Cognito
- `email` é obrigatório em `POST /auth/forgot-password`; ausência
  retorna 400 (`validation-error`)
- `email`, `code` e `newPassword` são obrigatórios em
  `POST /auth/reset-password`; ausência de qualquer um retorna 400
  (`validation-error`) — mesmo padrão do `ValidationBehavior` já usado
  pelas demais rotas
- `POST /auth/forgot-password` sempre retorna 200, independentemente de
  o email existir, já estar confirmado, ou o Cognito recusar o envio do
  código (decisão 1) — exceções não mapeadas explicitamente (ex.:
  `UserNotFoundException`, `InvalidParameterException`) são absorvidas e
  tratadas como sucesso; qualquer exceção verdadeiramente inesperada do
  SDK continua caindo no `GlobalExceptionHandler` (500), igual ao resto
  da API
- `POST /auth/reset-password` com código incorreto, havendo um código
  de reset pendente de verdade (`CodeMismatchException`) retorna 400
  (`invalid-reset-code`)
- `POST /auth/reset-password` com email inexistente (`ExpiredCodeException`,
  não `UserNotFoundException` — anti-enumeração do
  `prevent_user_existence_errors`) retorna 400 (`expired-reset-code`)
  — mesmo status 400 do código incorreto, sem revelar a diferença
  (decisão 2), ainda que o `type` específico não seja o mesmo
- `POST /auth/reset-password` com código expirado (`ExpiredCodeException`)
  retorna 400 (`expired-reset-code`)
- `POST /auth/reset-password` com `newPassword` fora da política de
  senha do Cognito (`InvalidPasswordException` — mínimo 8 caracteres
  **e** maiúscula, minúscula, número e símbolo, `cognito.tf` de hom/
  prod) retorna 400 (`bad-request`), reaproveitando
  `AuthErrors.Validation` já existente — o texto do protótipo
  (`26-nova-senha.png`, "Escolha uma senha com pelo menos 8 caracteres")
  não reflete a política real; frontend precisa espelhar a política
  completa (débito já registrado no backlog do frontend, fora do escopo
  desta feature)
- `POST /auth/reset-password` bem-sucedido dispara, na sequência, o
  email "senha alterada" (`03-senha-alterada.html`) via `ses:SendEmail`
  direto do backend (não passa pelo Custom Message trigger do Cognito —
  esse email não é gerado por nenhum fluxo nativo do Cognito).
  Placeholders do template: `{{email}}` (da própria request), `{{data}}`
  (data/hora do momento da redefinição), `{{dispositivo}}` (cabeçalho
  `User-Agent` cru da request, sem parsing — débito já registrado no
  backlog para refinar isso no futuro se o usuário quiser). Falha no
  envio deste email **não pode** derrubar a resposta de sucesso do reset
  — a senha já foi trocada de fato no Cognito; só loga (mesma filosofia
  defensiva já aplicada ao `EnsureAccountCommand` no
  `AccountTriggerHandler`, FEAT-19)
- Nenhuma mudança no TTL/política de expiração do código no Cognito —
  continua o padrão nativo do User Pool, sem tabela própria de OTP
- Nenhum mecanismo próprio de rate limit/brute-force para o código de
  reset (decisão 3)
- `POST /auth/login` continua exatamente como hoje — esta feature não
  altera esse endpoint

## User Stories

**US1 — Pedido de recuperação para email cadastrado**
- Given um usuário com conta confirmada no Cognito
- When ele chama `POST /auth/forgot-password` com o próprio `email`
- Then a API retorna 200 (sem corpo), e um código de 6 dígitos chega ao
  email dele (via `02-recuperacao-senha.html`)

**US2 — Pedido de recuperação para email inexistente ou não confirmado
não revela nada**
- Given um `email` que não existe no Cognito, ou que existe mas ainda
  não foi confirmado
- When alguém chama `POST /auth/forgot-password` com esse `email`
- Then a API retorna 200 (sem corpo) igualmente, sem indicar a
  diferença, e nenhum código é de fato enviado

**US3 — Redefinição com código correto**
- Given um usuário que pediu recuperação e recebeu o código
- When ele chama `POST /auth/reset-password` com `email`, o `code`
  correto e um `newPassword` dentro da política do Cognito
- Then a API retorna 200 (sem corpo), a senha é trocada no Cognito, um
  email de aviso ("senha alterada") é enviado, e o usuário passa a
  poder fazer `POST /auth/login` com a nova senha

**US4 — Código incorreto**
- Given um usuário que pediu recuperação
- When ele chama `POST /auth/reset-password` com um `code` que não
  confere
- Then a API retorna 400 (`invalid-reset-code`), a senha não muda

**US5 — Código expirado**
- Given um usuário cujo código de recuperação já passou do TTL do
  Cognito
- When ele chama `POST /auth/reset-password` com esse código
- Then a API retorna 400 (`expired-reset-code`)

**US6 — Email inexistente em reset-password**
- Given um `email` que nunca foi registrado
- When alguém chama `POST /auth/reset-password` com esse `email` e
  qualquer `code`/`newPassword`
- Then a API retorna 400 (`expired-reset-code`) — mesmo status 400 da
  US4, sem revelar que o email não existe (`type` específico corrigido
  em 2026-09-02, ver "Status")

**US7 — Senha fora da política**
- Given um usuário com código de recuperação válido
- When ele chama `POST /auth/reset-password` com um `newPassword` que
  não atende a política do Cognito (ex.: sem símbolo)
- Then a API retorna 400 (`bad-request`), a senha não muda

## Contratos da API

### POST /auth/forgot-password

Request:
```json
{
  "email": "neto@email.com"
}
```

Response 200: sem corpo (sempre, ver decisão 1 — inclusive quando o
email não existe ou já está confirmado sem pedido pendente).

Response 400 (parâmetro ausente):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Email é obrigatório."
}
```

### POST /auth/reset-password

Request:
```json
{
  "email": "neto@email.com",
  "code": "123456",
  "newPassword": "NovaSenha@2026"
}
```

Response 200: sem corpo.

Response 400 (parâmetro ausente):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de recuperação é obrigatório."
}
```

Response 400 (código incorreto — havendo um código de reset pendente
de verdade):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-reset-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de recuperação inválido."
}
```

Response 400 (código expirado, ou email inexistente — mesmo `type` nos
dois casos, corrigido em 2026-09-02, ver "Status"):
```json
{
  "type": "https://gastosapp.dev/errors/expired-reset-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de recuperação expirado."
}
```

Response 400 (senha fora da política):
```json
{
  "type": "https://gastosapp.dev/errors/bad-request",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo."
}
```

### Erros comuns

Formato padrão de erro do projeto
(`GastosApp.Api/Common/ResultHttpExtensions.cs`): `title` fixo e
genérico por tipo de erro (RFC 9457), mensagem específica sempre em
`detail`. Fonte de verdade exata: `backend/docs/openapi.json`.

## Critérios de aceite

- [x] `POST /auth/forgot-password` com email de usuário confirmado
      retorna 200 e dispara o código de recuperação por email (US1)
- [x] `POST /auth/forgot-password` com email inexistente ou não
      confirmado retorna 200 igualmente, sem revelar a diferença (US2)
- [x] `POST /auth/reset-password` com email, código e senha nova
      corretos retorna 200, troca a senha no Cognito, dispara o email de
      "senha alterada", e o usuário passa a logar com a nova senha (US3)
      — a parte "usuário passa a logar com a nova senha" não é
      verificada por teste automatizado de ponta a ponta (ver "Status")
- [x] `POST /auth/reset-password` com código incorreto retorna 400
      (`invalid-reset-code`) (US4)
- [x] `POST /auth/reset-password` com código expirado retorna 400
      (`expired-reset-code`) (US5)
- [x] `POST /auth/reset-password` com email inexistente retorna 400
      (`expired-reset-code`, corrigido em 2026-09-02 — ver "Status"),
      mesmo status 400 de código incorreto (US6)
- [x] `POST /auth/reset-password` com senha fora da política do Cognito
      retorna 400 (`bad-request`) (US7)
- [x] Ausência de `email` (forgot-password) ou `email`/`code`/
      `newPassword` (reset-password) retorna 400 (`validation-error`)
- [x] Falha no envio do email de "senha alterada" não impede a resposta
      de sucesso de `POST /auth/reset-password` (só loga)
- [x] `POST /auth/login` continua funcionando normalmente com a nova
      senha após um reset bem-sucedido, sem mudança de comportamento
      — não alterado nesta feature; sem teste específico de ponta a
      ponta (mesma ressalva do critério US3 acima)
- [x] Nenhuma mudança no TTL/política de expiração de código do Cognito
      User Pool (nem em `backend/infra/terraform/`)
- [x] Nenhuma mudança de IAM nova — reaproveita permissão SES já
      concedida na FEAT-33; único Terraform novo são os 2 parâmetros
      `Ses/SenderEmail` no Parameter Store (decisão do `/plan`, ver
      `plan.md`)
- [x] Template `frontend/design-system/emails/03-senha-alterada.html`
      ajustado para não depender de `{{nome}}` (decisão 4)
- [x] Os dois novos endpoints cobertos por teste de componente (mock de
      `IAuthService`/Cognito)
- [x] Os dois novos endpoints cobertos por teste integrado (pelo menos
      o fluxo de sucesso), rodado localmente via
      `backend/infra/lambda/run-local.sh` antes de a feature ser dada
      por concluída
- [x] Suíte completa de testes (unitário + componente) passando
- [x] `backend/docs/openapi.json` regenerado refletindo os dois novos
      endpoints

## Status

Implementação concluída (todas as 57 tasks de `tasks.md`). Suíte
completa: 502 unit + 224 componente + 34 integrado (todos passando,
inclusive contra o binário Native AOT via `run-local.sh`).

O `cognito-local` v5.3.0 implementa `ForgotPassword`/
`ConfirmForgotPassword` — os 4 testes integrados novos rodam
localmente sem erro. **Atualização de 2026-09-02, após a primeira
execução real de `backend-integration-tests-hom.yml` (0 runs
anteriores):** 2 dos 4 (`ResetPassword_CodigoIncorreto_Retorna400`,
`ResetPassword_EmailInexistente_Retorna400`) precisaram de correção —
mesmo achado documentado em
`backend/specs/FEAT-35-confirmacao-cadastro-otp/spec.md` ("Status"),
aplicado aqui:
- `ResetPassword_EmailInexistente_Retorna400`: `expired-reset-code`,
  não `invalid-reset-code` (anti-enumeração do
  `prevent_user_existence_errors`, `ExpiredCodeException` em vez de
  `UserNotFoundException`) — ganhou guarda `IsLocal` (`cognito-local`
  ainda lança `UserNotFoundException` de fato, diverge de hom/prod).
- `ResetPassword_CodigoIncorreto_Retorna400`: bug de teste, não de
  contrato — nunca chamava `POST /auth/forgot-password` antes do
  reset, então não havia código pendente de verdade pra comparar; o
  Cognito real cai em `ExpiredCodeException` nesse caso (mesma
  anti-enumeração acima), não `CodeMismatchException`. Corrigido
  gerando um código pendente real primeiro — segue sem guarda
  `IsLocal`, passa nos dois ambientes.

Nenhuma mudança de código foi necessária em `CognitoAuthService` — o
mapeamento de exceção já estava correto, só a suposição de qual
exceção real o Cognito lança pra esses 2 cenários estava errada
(validada só contra `cognito-local` até então).

**Ponto de confirmação 2 do `plan.md`** (viabilidade do teste integrado
de "senha fora da política"): investigado empiricamente durante a
implementação (`curl` direto contra a Api local + `cognito-local`,
conta real registrada e confirmada) — confirmado que
`ConfirmForgotPassword` valida o **código antes da senha**: código
errado + senha fraca simultaneamente ainda retorna 400
`invalid-reset-code`, nunca `bad-request`. Como a suíte não tem acesso
ao código real de recuperação (só chega por email), não há como forçar
o caminho "código correto + senha fora da política" — não é viável
como teste integrado. Coberto pelos testes unitário
(`ConfirmForgotPasswordAsync_ShouldReturnValidationError_
WhenCognitoThrowsInvalidPasswordException`) e de componente
(`ResetPassword_QuandoAuthServiceRetornaErro_PropagaProblemDetails`).
Comentário equivalente registrado em `AuthFlowTests.cs`.

**2 novos parâmetros SSM `String`** (`/GastosApp/Ses/SenderEmail` e
`/GastosApp/Hom/Ses/SenderEmail`) criados em `parameter-store.tf` de
cada ambiente. **`/GastosApp/Hom/Ses/SenderEmail` já aplicado e
confirmado na AWS de hom (2026-09-02)** — a versão original do `.tf`
referenciava `aws_cognito_user_pool.main.email_configuration[...]` ao
vivo, o que gerava diff perpétuo (a AWS devolve esse atributo com
encoding MIME quando o texto tem acentuação, diferente do literal do
`.tf`) e arrastava `aws_lambda_function.custom_message_trigger` pra
dentro de qualquer apply targeted deste recurso, com risco de reverter
o código real da Lambda pro zip local desatualizado (deploy real
acontece fora do Terraform). Corrigido trocando pro mesmo literal já
usado em `email_configuration` — `terraform plan` completo (sem
`-target`) confirma que o recurso fica estável, sem entrar em diff.
Prod ainda não aplicado (código também não deployado lá ainda).

**Fluxo "código correto → senha trocada → login com a nova senha" de
ponta a ponta não é coberto por teste automatizado** — mesma limitação
já aceita na FEAT-35 (a suíte não tem acesso ao código de 6 dígitos
enviado por email); os critérios de aceite correspondentes (US3 e o
item de `POST /auth/login`) foram marcados com essa ressalva explícita.

Foi necessário também ajustar um teste-canário pré-existente
(`AddApplicationServices_ShouldNotRegisterAnyOtherValidator_
BeyondTheKnownFourteen` → `...Sixteen`, em `ApplicationExtensionsTests.cs`)
para refletir os 2 validators novos — não estava listado como task
própria em `tasks.md`, mas é manutenção esperada desse teste de
regressão.

## Fora do escopo

- Qualquer mudança no TTL/expiração real do código no Cognito — o
  cooldown de 60s do frontend continua sendo só uma UX de reenvio, não
  uma garantia do backend
- Rate limiting/brute-force próprio para tentativas de reset — decisão 3,
  fica a cargo das proteções nativas do Cognito
- Personalização por nome no email de "senha alterada" — decisão 4
- Parsing de `User-Agent` em `{{dispositivo}}` — usa o cabeçalho cru
  (débito já registrado no backlog)
- Qualquer mudança de contrato em `POST /auth/register`,
  `POST /auth/login`, `GET /auth/me`, `POST /auth/refresh`,
  `POST /auth/logout`, `POST /auth/confirm` ou
  `POST /auth/resend-confirmation`
- Correção do `InvalidPasswordException` não tratado em
  `POST /auth/register` — débito técnico separado, registrado no
  backlog durante este `/specify`
