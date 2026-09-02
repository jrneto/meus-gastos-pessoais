# FEAT-34: Custom Message trigger do Cognito (e-mails de auth com HTML)

## Objetivo

Substituir o corpo padrão (texto genérico, sem estilo, sem marca) dos
e-mails que o Cognito envia nativamente para confirmação de cadastro,
reenvio de código e recuperação de senha pelos templates HTML com a
marca do jrn.expenses (`frontend/design-system/emails/
01-confirmacao-cadastro.html` e `02-recuperacao-senha.html`), via um
novo handler do trigger `CustomMessage` do Cognito — ao lado do
`AccountTriggerHandler` (`PostConfirmation`) já existente. O envio em
si continua sendo feito pelo próprio Cognito, através do SES já
configurado na FEAT-33 — este trigger só decide o assunto e o corpo da
mensagem antes de o Cognito despachá-la; nunca chama
`ses:SendEmail`/`SendRawEmail` diretamente.

## Contexto

Parte da leva "Autenticação — área não logada" combinada em 2026-09-01
(`backend/docs/backlog.md`), que formalizou a infraestrutura de e-mail
(FEAT-33, concluída) como pré-requisito de qualquer e-mail com HTML
próprio. Decisões já fechadas com o usuário, válidas para esta feature:

- O timer de 60s do protótipo é só um cooldown de reenvio no frontend,
  não uma expiração real de código — o backend usa os fluxos nativos do
  Cognito (TTL padrão deles), sem tabela própria de OTP. Este trigger
  nunca gera nem valida código — só formata a mensagem em torno do
  código que o próprio Cognito já gerou.
- Nenhuma execução Terraform sem aprovação explícita do usuário — vale
  para o wiring do trigger `CustomMessage` no `aws_cognito_user_pool` de
  hom/prod.

**Mapeamento de `TriggerSource` (Cognito) → template:**

| TriggerSource | Quando ocorre | Template |
| --- | --- | --- |
| `CustomMessage_SignUp` | `POST /auth/register` (`SignUpAsync`) | `01-confirmacao-cadastro.html` |
| `CustomMessage_ResendCode` | Reenvio do código de confirmação (`ResendConfirmationCodeAsync`, endpoint ainda não existe — ver FEAT-35) | `01-confirmacao-cadastro.html` |
| `CustomMessage_ForgotPassword` | Pedido de recuperação de senha (`ForgotPasswordAsync`, endpoint ainda não existe — ver FEAT-36) | `02-recuperacao-senha.html` |

Qualquer outro `TriggerSource` de `CustomMessage` (ex.:
`CustomMessage_AdminCreateUser`, `CustomMessage_UpdateUserAttribute`,
`CustomMessage_VerifyUserAttribute`) fica fora do escopo — o Cognito
continua usando o texto padrão dele nesses casos.

**Achado técnico que muda um requisito da feature — `{{nome}}` exige
mudar `RegisterAsync`:** o `CustomMessage_SignUp` é invocado pelo
Cognito de forma síncrona *dentro* da própria chamada `SignUpAsync`
(`CognitoAuthService.RegisterAsync`) — ou seja, **antes** de
`RegisterUserCommandHandler` retomar o controle e gravar o perfil
(nome/telefone/CPF) no DynamoDB (`_userProfileRepository.CreateAsync`).
Além do timing, o Cognito só expõe ao trigger os `userAttributes` que
ele mesmo já tem armazenados para aquele usuário — hoje o `SignUpAsync`
manda só `email` (`CognitoAuthService.RegisterAsync`), nunca `name`.
Sem mudança, `{{nome}}` nunca teria valor real disponível em nenhum dos
3 `TriggerSource`, nem no `SignUp` (perfil ainda não existe) nem em
`ResendCode`/`ForgotPassword` (perfil já existe no DynamoDB, mas o
Cognito não o consulta — só enxerga seus próprios atributos).

Decisão fechada com o usuário para viabilizar `{{nome}}`: `SignUpAsync`
passa a enviar também `name` (atributo **padrão** do Cognito) junto com
`email` no momento do cadastro — mudança aditiva em
`CognitoAuthService.RegisterAsync`, sem impacto no contrato de
`POST /auth/register` (nenhum campo novo de request/response). Uma vez
setado no `SignUp`, o Cognito guarda esse atributo no usuário e ele já
fica disponível também em `ResendCode`/`ForgotPassword` para o mesmo
usuário. Isso **não** substitui o perfil no DynamoDB como fonte de
verdade do nome (continua sendo gravado ali normalmente, inclusive
`_userProfileRepository.CreateAsync` continua necessário — é lá que
mora a checagem de CPF único e o CPF/telefone, que nunca podem virar
atributo do Cognito sem redefinir o schema do User Pool, decisão já
fechada na FEAT-26) — é só a mesma informação replicada também para o
Cognito, unicamente para permitir personalizar estes e-mails.

**URLs dos templates:** `01-confirmacao-cadastro.html` e
`02-recuperacao-senha.html` usam `app.jrnexpenses.com.br` como
placeholder nos links. A FEAT-33 deixou a correção para o domínio real
(`jrnexpenses.com`, sem `.br`) explicitamente para quem consumisse cada
template — esta feature é quem consome os dois, então corrige aqui.

Depende de FEAT-33 (SES, concluída). Recomendado antes de FEAT-35/36
para que os endpoints de confirmação/recuperação já nasçam com e-mail
de marca própria, mas não é bloqueio técnico — o Cognito já pode
invocar `SignUp`/`ForgotPassword` hoje (via `POST /auth/register` já
existente, ou via console/CLI do Cognito para os fluxos que ainda não
têm endpoint).

## Requisitos de negócio

- `CustomMessage_SignUp` e `CustomMessage_ResendCode` usam o mesmo
  template (`01-confirmacao-cadastro.html`) — ambos comunicam um código
  novo de confirmação de cadastro.
- `CustomMessage_ForgotPassword` usa `02-recuperacao-senha.html`.
- `{{codigo}}` é substituído pelo código que o próprio Cognito já gera
  e injeta no evento — nunca gerado, antecipado ou validado por este
  trigger.
- `{{nome}}` é substituído pelo atributo `name` do usuário no Cognito —
  exige que `SignUpAsync` passe a enviá-lo (ver Contexto). Sem esse
  atributo disponível (situação inesperada), o trigger aplica um
  fallback textual (ex.: saudação sem nome) em vez de deixar
  `{{nome}}` literal no e-mail.
- `{{email}}` é substituído pelo e-mail do próprio usuário (atributo já
  existente no Cognito).
- O assunto do e-mail também é customizado por este trigger — texto
  fixo, sem `{{codigo}}`. **Revisto durante a validação ao vivo em hom:**
  o Cognito não substitui o placeholder `{####}` em `emailSubject` (só
  em `emailMessage`), diferente do sugerido originalmente em
  `frontend/design-system/emails/README.md` (que assumia o mesmo
  comportamento nos dois campos); o código continua visível e em
  destaque no corpo do e-mail.
- As URLs `app.jrnexpenses.com.br` dos templates são corrigidas para o
  domínio real (`jrnexpenses.com`) antes de o HTML ser usado por este
  trigger.
- Falha inesperada ao montar a mensagem customizada nunca pode impedir
  a operação de negócio em andamento (`SignUp`/`ResendConfirmationCode`/
  `ForgotPassword`) — nesse caso o trigger devolve o evento sem alterar
  o texto, e o Cognito envia o e-mail padrão dele como fallback; nunca
  propaga exceção (mesma filosofia defensiva já aplicada ao
  `AccountTriggerHandler`/`PostConfirmation`, só loga).
- Nenhum `TriggerSource` de `CustomMessage` fora dos 3 listados é
  afetado — continuam com o texto padrão do Cognito.
- Este trigger não envia e-mail por conta própria em nenhuma hipótese
  (`ses:SendEmail`/`SendRawEmail` fora do seu escopo) — quem envia
  continua sendo o Cognito, via SES já configurado na FEAT-33.

## User Stories

**US1 — Cadastro (SignUp) recebe e-mail com HTML de marca própria**
- Given um usuário preenchendo `POST /auth/register` com nome, e-mail e
  senha válidos
- When o Cognito invoca `CustomMessage_SignUp` como parte do
  `SignUpAsync`
- Then o e-mail enviado usa o HTML de `01-confirmacao-cadastro.html`,
  com `{{codigo}}` igual ao código gerado pelo Cognito, `{{nome}}` igual
  ao nome informado no cadastro e `{{email}}` igual ao e-mail do
  usuário

**US2 — Reenvio de código usa o mesmo template de confirmação**
- Given um usuário não confirmado, cujo reenvio de código é disparado
  (via Cognito, independente de já existir endpoint próprio — ver
  FEAT-35)
- When o Cognito invoca `CustomMessage_ResendCode`
- Then o e-mail enviado usa o mesmo HTML de
  `01-confirmacao-cadastro.html`, com `{{codigo}}`/`{{nome}}`/`{{email}}`
  resolvidos da mesma forma que no cadastro

**US3 — Recuperação de senha recebe e-mail com HTML próprio**
- Given um usuário que tem uma recuperação de senha disparada (via
  Cognito, independente de já existir endpoint próprio — ver FEAT-36)
- When o Cognito invoca `CustomMessage_ForgotPassword`
- Then o e-mail enviado usa o HTML de `02-recuperacao-senha.html`, com
  `{{codigo}}`, `{{nome}}` e `{{email}}` resolvidos

**US4 — Falha no formatador não bloqueia o fluxo de negócio**
- Given uma falha inesperada no handler deste trigger (ex.: exceção ao
  processar o evento)
- When o Cognito invoca qualquer um dos 3 `TriggerSource` cobertos
- Then a operação de negócio (`SignUp`/`ResendConfirmationCode`/
  `ForgotPassword`) continua completando normalmente — o e-mail sai com
  o texto padrão do Cognito, e nada é bloqueado por causa deste trigger

**US5 — `TriggerSource` fora do escopo não é afetado**
- Given qualquer `TriggerSource` de `CustomMessage` não coberto por esta
  feature (ex.: criação administrativa de usuário)
- When o Cognito invoca o trigger
- Then o corpo e o assunto do e-mail permanecem exatamente os padrões
  do Cognito, sem qualquer alteração

## Contratos da API

Esta feature não introduz nem altera nenhum endpoint HTTP — o
comportamento observável é o conteúdo do e-mail que chega ao usuário
nos 3 fluxos cobertos (cadastro, reenvio de código, recuperação de
senha), nunca a resposta de uma rota.

- `POST /auth/register` (FEAT-01/FEAT-26): request e response
  permanecem idênticos aos já documentados em
  `backend/docs/openapi.json`. O único efeito interno (não observável
  no contrato HTTP) é `SignUpAsync` passar a também registrar `name`
  como atributo do usuário no Cognito.
- Nenhum endpoint novo é criado por esta feature — `POST
  /auth/resend-confirmation`, `POST /auth/forgot-password` e `POST
  /auth/reset-password` são escopo das FEAT-35/36, que passam a
  disparar os `TriggerSource` `CustomMessage_ResendCode`/
  `CustomMessage_ForgotPassword` já cobertos aqui.

## Critérios de aceite

- [x] `CustomMessage_SignUp`: e-mail chega com o HTML de
      `01-confirmacao-cadastro.html`, `{{codigo}}`/`{{nome}}`/`{{email}}`
      resolvidos corretamente — validado manualmente em hom via `POST
      /auth/register`. `{{codigo}}`/`{{email}}`/HTML/URLs confirmados ao
      vivo; `{{nome}}` confirmado via teste automatizado
      (`CustomMessageTriggerHandlerTests`) — validação end-to-end em hom
      fica pendente até o merge desta branch pra `develop` (a API de hom
      só reflete o código de `develop`, que ainda não manda `name` pro
      Cognito nesta rodada de testes)
- [x] `CustomMessage_ResendCode`: mesmo template, `{{codigo}}`/`{{nome}}`/
      `{{email}}` resolvidos — validado manualmente (reenvio via
      `aws cognito-idp resend-confirmation-code`, já que `POST
      /auth/resend-confirmation` ainda não existe). Mesma ressalva de
      `{{nome}}` pendente pós-merge.
- [x] `CustomMessage_ForgotPassword`: e-mail chega com o HTML de
      `02-recuperacao-senha.html`, `{{codigo}}`/`{{nome}}`/`{{email}}`
      resolvidos — validado manualmente via `aws cognito-idp
      forgot-password`, já que `POST /auth/forgot-password` ainda não
      existe
- [x] Falha simulada no handler não impede a conclusão do
      `SignUp`/`ResendConfirmationCode`/`ForgotPassword` — e-mail sai
      com o texto padrão do Cognito nesse cenário. Validado via teste
      automatizado (`HandleAsync_ShouldNeverPropagateFailure_WhenFormattingThrows`);
      decisão do usuário de não repetir a simulação contra hom real
- [x] `TriggerSource` de `CustomMessage` fora dos 3 listados (ex.:
      `AdminCreateUser`) continua com o texto padrão do Cognito, sem
      regressão — validado manualmente em hom
- [x] URLs dos templates corrigidas de `app.jrnexpenses.com.br` para o
      domínio real (`jrnexpenses.com`)
- [x] `SignUpAsync` passa a enviar `name` como atributo do Cognito, sem
      alterar request/response de `POST /auth/register`
      (`backend/docs/openapi.json` sem diff de contrato)
- [x] Teste de componente cobrindo o novo handler (evento simulado do
      Cognito para os 3 `TriggerSource` cobertos, `{{nome}}` ausente
      como caso defensivo, e falha inesperada com fallback), seguindo o
      padrão de `AccountTriggerHandlerTests`
- [x] `terraform apply` que liga o trigger `CustomMessage` do
      `aws_cognito_user_pool` (hom e depois prod) só executado após
      aprovação explícita do usuário

## Fora do escopo

- `POST /auth/confirm`, `POST /auth/resend-confirmation` (FEAT-35) e
  `POST /auth/forgot-password`/`POST /auth/reset-password` (FEAT-36) —
  endpoints que de fato disparam esses fluxos de negócio no Cognito;
  esta feature só cobre a formatação do e-mail que o Cognito já sabe
  disparar hoje (via `POST /auth/register` ou via console/CLI para os
  outros dois fluxos)
- E-mail de "senha alterada" (`03-senha-alterada.html`) e "boas-vindas"
  (`04-boas-vindas.html`) — não passam pelo trigger `CustomMessage`, são
  enviados diretamente pelo backend via `ses:SendEmail` (FEAT-36/37)
- Qualquer `TriggerSource` de `CustomMessage` além dos 3 listados (ex.:
  `AdminCreateUser`, `UpdateUserAttribute`, `VerifyUserAttribute`)
- Corrigir o e-mail caindo em spam (falta de SPF/MAIL FROM/DMARC) —
  débito técnico já registrado em `backend/docs/backlog.md`, resolvido
  independente desta feature
- Qualquer mudança de TTL/expiração real do código OTP — decisão já
  fechada de usar o TTL nativo do Cognito, sem mecanismo próprio
- Mover telefone/CPF para atributos do Cognito — continuam só no
  perfil do DynamoDB (decisão da FEAT-26 mantida); apenas `name` passa
  a ser replicado também para o Cognito, só para viabilizar estes
  e-mails

## Status

Implementado conforme `plan.md`/`tasks.md`. `IAuthService.RegisterAsync`/
`CognitoAuthService.RegisterAsync`/`RegisterUserCommandHandler` passam a
enviar `name` como atributo padrão do Cognito, ao lado de `email`, sem
alterar o contrato de `POST /auth/register`. Novo projeto
`GastosApp.CognitoTriggers.CustomMessage` (Native AOT, sem
`ProjectReference` pra `Application`/`Infrastructure`) com
`CustomMessageTriggerHandler`, `EmailTemplateProvider` (HTMLs embutidos
como `EmbeddedResource`, copiados de `frontend/design-system/emails/`
com URLs corrigidas para `jrnexpenses.com`) e `Function.cs`, adicionado
à `GastosApp.sln`.

Terraform aplicado em hom e prod (IAM Role dedicada só com
`logs:CreateLogStream`/`PutLogEvents`, Lambda, `aws_lambda_permission`
e `lambda_config.custom_message` no User Pool), com aprovação explícita
do usuário antes de cada `apply`. Variáveis
`CUSTOM_MESSAGE_TRIGGER_FUNCTION_NAME` cadastradas nos GitHub
Environments `backend-hom`/`backend-prod`. Workflows
`backend-deploy-custom-message-trigger-{hom,prod}.yml` criados,
espelhando o padrão de `backend-deploy-account-trigger-*`.

**Achado real durante a validação ao vivo em hom (task 25):** o Cognito
não substitui o placeholder `{####}` em `emailSubject`, só em
`emailMessage` — divergência da decisão técnica 5 do `plan.md`. Corrigido
trocando o assunto por texto fixo, sem `{{codigo}}` ("Confirme seu
cadastro no jrn.expenses" / "Redefinição de senha solicitada"); o
código permanece visível e em destaque no corpo do e-mail. Fix
implantado manualmente em hom (`aws lambda update-function-code`, fora
da esteira normal) pra permitir revalidar antes do merge, e revalidado
com sucesso via `CustomMessage_ResendCode`.

`{{nome}}` foi validado por teste automatizado
(`CustomMessageTriggerHandlerTests`) e pela leitura direta do schema do
User Pool de hom (atributo `name` já suportado nativamente, não precisa
de mudança de Terraform), mas a validação end-to-end em hom ficou
pendente: a API de hom, no momento dos testes, ainda rodava o código de
`develop` (sem a mudança desta branch), então o Cognito não tinha
`name` armazenado para o usuário de teste. Fica como validação
pendente pra depois do merge desta branch + deploy da API em hom.

Suíte completa (`dotnet test GastosApp.sln --filter
"Category!=Integration"`): 479 (UnitTests) + 207 (ComponentTests) = 686
testes, 100% passando. Testes integrados locais do módulo Auth (via
`run-local.sh`/RIE + cognito-local/LocalStack): 4 testes passando,
confirmando que a mudança em `RegisterAsync` não quebra o fluxo real de
cadastro. `backend/docs/openapi.json` regenerado — `git diff` confirma
zero diferença de contrato (só normalização de fim de linha).

Dois usuários de teste ficaram no User Pool de hom
(`reato.neto@gmail.com`, `reato.neto+admintest@gmail.com`) — decisão do
usuário de não limpar agora.
