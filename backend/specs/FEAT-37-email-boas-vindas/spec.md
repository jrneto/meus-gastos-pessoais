# FEAT-37: E-mail de boas-vindas

## Objetivo

Assim que a conta de um usuário é criada (confirmação de cadastro bem-
sucedida), enviar um e-mail de boas-vindas com a marca do produto,
confirmando que a conta está ativa e sugerindo os primeiros passos —
fechando a última peça da leva de e-mails transacionais prevista desde
a FEAT-33 (confirmação, recuperação, senha alterada, boas-vindas).

## Contexto

Item do backlog (`backend/docs/backlog.md`), substitui a antiga
FEAT-27 (que dependia de uma infraestrutura de e-mail ainda
inexistente na época). Depende só de FEAT-33 (infraestrutura SES) e
FEAT-19 (Account/Membership + trigger `Post Confirmation`), ambas já
concluídas — não depende de FEAT-34/35/36.

**O que já está pronto e esta feature reaproveita sem precisar de
infraestrutura nova:**
- O trigger `Post Confirmation` do Cognito já existe
  (`AccountTriggerHandler`, Lambda `GastosApp.CognitoTriggers`, FEAT-19)
  e já despacha `EnsureAccountCommand` com sucesso defensivo (falha não
  bloqueia a confirmação, só loga).
- `IEmailSender`/`SesEmailService` (Infrastructure, FEAT-36) já são
  genéricos o bastante para esta feature reaproveitar sem mudança —
  foi desenhado para isso desde a FEAT-33.
- **IAM `ses:SendEmail`/`ses:SendRawEmail` para a Lambda de trigger de
  conta já foi concedido** em `lambda-account-trigger.tf` de hom e prod,
  desde a FEAT-33 (`aws_iam_role_policy.account_trigger_lambda_exec`,
  Sid `SesSendEmail`) — achado confirmado durante este `/specify`,
  contrariando a suposição original do item no backlog ("precisa
  acrescentar `ses:SendEmail` na IAM role"). **Nenhuma mudança de IAM
  nesta feature.**

**O que falta e este `/specify` já identificou (detalhar no `plan.md`):**
- A Lambda de trigger de conta **não lê Parameter Store** (decisão
  deliberada da FEAT-19, para não precisar de `ssm:GetParametersByPath`
  nem de uma chamada de rede a mais no cold start — `Function.cs` só
  usa variáveis de ambiente). `SesOptions.SenderEmail` (lido de
  configuração pela seção `Ses`) hoje só chega à Lambda da API via
  Parameter Store (`/GastosApp/Ses/SenderEmail`, FEAT-36). Para esta
  Lambda, precisa de uma variável de ambiente nova
  (`Ses__SenderEmail`) em `lambda-account-trigger.tf` de cada
  ambiente, com o mesmo literal já usado em `parameter-store.tf`/
  `email_configuration` do Cognito — mesmo padrão de literal fixo (não
  referência ao atributo ao vivo do User Pool) já adotado na FEAT-36
  para evitar diff perpétuo no Terraform.
- `frontend/design-system/emails/04-boas-vindas.html` usa `{{nome}}` no
  título ("Bem-vindo, {{nome}}."), mas o evento `Post Confirmation` do
  Cognito só carrega `sub`/`email` em `UserAttributes` — o nome real do
  usuário fica em `UserProfile` (DynamoDB, FEAT-26), não em atributo do
  Cognito.
- O mesmo template usa o domínio `jrnexpenses.com.br` (link "Começar
  agora", link "Gerenciar preferências", e-mail de suporte) — o domínio
  real do projeto é `jrnexpenses.com` (sem ".br", confirmado em
  `parameter-store.tf` e na hosted zone Route53 do frontend).

**Decisões já confirmadas com o usuário durante este `/specify`:**

1. **Nome real via `UserProfile`, não remover a personalização.**
   `AccountTriggerHandler` passa a consultar `IUserProfileRepository.
   FindByUserIdAsync` (mesma tabela DynamoDB, já com permissão IAM —
   sem policy nova) para obter o nome cadastrado no registro (FEAT-26)
   e preencher `{{nome}}` de verdade.
2. **Sem perfil, sem e-mail — nenhum fallback de saudação genérica**
   (revisto após o `/specify` inicial, a pedido do usuário). Um usuário
   confirmado sem `UserProfile` correspondente (ex.: criado direto no
   console Cognito, fora do fluxo `POST /auth/register`) é uma anomalia
   — a FEAT-31 já bloqueia o login desse usuário (403
   `profile-incomplete`) justamente por ele não ter cadastro completo.
   Esta feature não trata esse cenário como um caminho válido a
   suportar com conteúdo degradado: perfil não encontrado é tratado
   como qualquer outra falha de envio (mesmo mecanismo defensivo
   log-only, ver requisito abaixo) — o e-mail simplesmente não é
   enviado, sem texto alternativo no lugar do nome.
3. **Corrigir o domínio no template antes de ir para produção.**
   `04-boas-vindas.html` é ajustado nesta feature, trocando
   `jrnexpenses.com.br` por `jrnexpenses.com` nos 3 lugares (CTA,
   link de preferências, e-mail de suporte).

## Requisitos de negócio

- Após `EnsureAccountCommand` retornar sucesso dentro do
  `AccountTriggerHandler` (trigger `Post Confirmation` do Cognito), o
  handler dispara o envio do e-mail de boas-vindas
  (`04-boas-vindas.html`) via `ses:SendEmail` direto (mesmo padrão da
  FEAT-36 — não passa pelo `CustomMessage` trigger, que só existe para
  e-mails gerados nativamente pelo Cognito).
- O e-mail só é enviado quando `EnsureAccountCommand` de fato cria a
  conta (`AlreadyExisted: false`) — reenvio de confirmação
  (`ResendConfirmationCode`, FEAT-35) ou uma nova chamada idempotente
  ao trigger para uma conta já existente **não** reenvia boas-vindas.
- Placeholders do template:
  - `{{nome}}`: nome cadastrado em `UserProfile` (via
    `IUserProfileRepository.FindByUserIdAsync`, buscado pelo `userId`
    do próprio evento).
  - `{{email}}`: e-mail do próprio evento `Post Confirmation`
    (`UserAttributes["email"]`, já validado como presente pelo mesmo
    guard que `EnsureAccountCommand` usa).
- **Ausência de `UserProfile` não é um caminho suportado com conteúdo
  alternativo** (decisão 2): tratada como falha de envio, sem e-mail
  degradado — mesmo mecanismo defensivo do bullet seguinte.
- Falha ao buscar o perfil (`IUserProfileRepository`), a ausência dele,
  ou falha ao enviar o e-mail (SES) **não pode** bloquear nem reverter
  a criação da conta — mesma filosofia defensiva já aplicada a
  `EnsureAccountCommand` dentro de `AccountTriggerHandler` (FEAT-19):
  só loga, o trigger sempre retorna sucesso ao Cognito.
- `frontend/design-system/emails/04-boas-vindas.html` é ajustado para
  usar o domínio real (`jrnexpenses.com`) nos 3 links/endereços citados
  acima (decisão 3).
- Nenhuma mudança no contrato de `POST /auth/register`,
  `POST /auth/confirm`, `POST /auth/resend-confirmation`, no login, ou
  em qualquer outro endpoint — esta feature não expõe rota HTTP nova,
  só estende o trigger assíncrono já existente.

## User Stories

**US1 — Conta nova criada com perfil completo**
- Given um usuário que se registrou via `POST /auth/register` (nome,
  telefone, CPF já salvos em `UserProfile`, FEAT-26) e confirma o
  cadastro
- When o trigger `Post Confirmation` roda e `EnsureAccountCommand` cria
  a conta pela primeira vez (`AlreadyExisted: false`)
- Then um e-mail de boas-vindas chega ao endereço cadastrado, com o
  nome real do usuário em "Bem-vindo, {{nome}}."

**US2 — Conta nova criada sem perfil (usuário criado fora do
`/auth/register`)**
- Given um usuário confirmado no Cognito sem `UserProfile` correspondente
  no DynamoDB (mesmo cenário de borda da FEAT-31 — ex.: criado
  diretamente no console AWS)
- When o trigger `Post Confirmation` roda e `EnsureAccountCommand` cria
  a conta pela primeira vez
- Then nenhum e-mail de boas-vindas é enviado (tratado como falha,
  só loga), mas a confirmação do cadastro e a criação da conta
  continuam funcionando normalmente — mesmo efeito prático de US4

**US3 — Conta já existente (idempotência)**
- Given um usuário cuja conta já foi criada anteriormente (ex.:
  segunda invocação do trigger para o mesmo `userId`, ou confirmação
  via `ConfirmForgotPassword`, que também dispara `Post Confirmation`)
- When o trigger roda novamente e `EnsureAccountCommand` resolve a
  conta já existente (`AlreadyExisted: true`)
- Then nenhum e-mail de boas-vindas novo é enviado

**US4 — Falha no envio do e-mail não afeta a confirmação**
- Given qualquer falha ao buscar o perfil ou ao chamar `ses:SendEmail`
  (ex.: SES indisponível, DynamoDB temporariamente inacessível)
- When o trigger `Post Confirmation` roda
- Then a confirmação do usuário e a criação da conta continuam
  funcionando normalmente — só a falha é logada, o Cognito recebe
  sucesso do trigger

## Comportamento observável

Esta feature não introduz nem altera nenhum endpoint HTTP — o
comportamento observável é assíncrono, via e-mail, disparado pelo
trigger `Post Confirmation` do Cognito (`GastosApp.CognitoTriggers`).
Não há request/response de API para documentar; `backend/docs/
openapi.json` não muda.

Efeito colateral esperado por confirmação de cadastro bem-sucedida
(primeira vez): um e-mail HTML chega ao endereço cadastrado, remetente
`jrn.expenses <no-reply@jrnexpenses.com>` (prod) ou `jrn.expenses
(homologação) <no-reply@hom.jrnexpenses.com>` (hom) — mesmo remetente
já usado pelo e-mail de "senha alterada" (FEAT-36), assunto igual ao
`<title>` do template ("Bem-vindo ao jrn.expenses").

## Critérios de aceite

- [ ] Conta criada pela primeira vez (`AlreadyExisted: false`) dispara
      o envio do e-mail de boas-vindas (US1)
- [ ] Usuário com `UserProfile` cadastrado recebe o e-mail com o nome
      real em `{{nome}}` (US1)
- [ ] Usuário sem `UserProfile` não recebe e-mail de boas-vindas (tratado
      como falha, sem conteúdo degradado), sem travar a confirmação nem
      a criação da conta (US2)
- [ ] Conta já existente (`AlreadyExisted: true`) não gera novo envio
      de e-mail de boas-vindas (US3)
- [ ] Falha ao buscar perfil ou ao enviar o e-mail não impede a
      confirmação do cadastro nem a criação da conta — só loga (US4)
- [ ] `frontend/design-system/emails/04-boas-vindas.html` corrigido
      para usar `jrnexpenses.com` (sem ".br") nos 3 lugares (CTA, link
      de preferências, e-mail de suporte) — decisão 3
- [ ] `Ses__SenderEmail` (variável de ambiente) adicionada a
      `lambda-account-trigger.tf` de hom e prod, com o mesmo literal já
      usado em `parameter-store.tf`/`email_configuration` do Cognito —
      detalhar viabilidade/abordagem exata no `plan.md`
- [ ] Nenhuma mudança de IAM nova (permissão já concedida na FEAT-33)
- [ ] Fluxo coberto por teste unitário (`AccountTriggerHandler`,
      mock de `IUserProfileRepository`/`IEmailSender` ou equivalente)
- [ ] Suíte completa de testes (unitário + componente) passando
- [ ] Teste integrado avaliado no `plan.md`: `AccountTriggerHandler`
      roda fora do fluxo HTTP padrão (invocado diretamente nos testes
      de unidade hoje, ver `AccountTriggerHandlerManualDebug.cs`) — se
      não for viável como teste integrado automatizado, documentar a
      limitação explicitamente (mesmo padrão já aceito em FEAT-35/36
      para trechos não verificáveis pela suíte)
- [ ] `backend/docs/openapi.json` não muda (sem contrato de API novo) —
      confirmar que nenhuma regeneração é necessária

## Fora do escopo

- Qualquer mudança de contrato em `POST /auth/register`,
  `POST /auth/confirm`, `POST /auth/resend-confirmation` ou no login
- Página de "gerenciar preferências de e-mail" (link presente no
  template, mas sem rota/funcionalidade correspondente hoje — link
  apenas corrigido para o domínio certo, não implementado)
- Qualquer mecanismo de reenvio manual do e-mail de boas-vindas
- Personalização adicional além do nome (ex.: primeira categoria
  usada, dados de uso) — fora de escopo, o e-mail é estático além de
  `{{nome}}`/`{{email}}`
- SPF/MAIL FROM customizado e DMARC — débito técnico já registrado no
  backlog (FEAT-33), não resolvido por esta feature
