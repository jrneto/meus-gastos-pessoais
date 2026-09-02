# Backlog — Backend

Sequência combinada em 2026-08-22 para o backend alcançar tudo que o
design system (`frontend/design-system/screenshots/`) já assume, com
todas as rotas prontas pro frontend consumir. Cada linha da seção
**Features** vira uma `spec.md` própria em
`backend/specs/{FEAT-XX-nome}/` via `/specify`, seguindo o fluxo normal
(`/specify` → `/plan` → `/tasks` → implementar → `/review`). Ver
`backend/docs/README.md` para o processo.

**Como usar este arquivo:** o backlog é dividido em 4 seções —
**Features**, **Bugs**, **Débitos técnicos e melhorias futuras** e
**Compliance (LGPD)**. Todo item leva um checkbox: `- [x]` quando já
implementado/testado/revisado, `- [ ]` enquanto pendente. Ao terminar um
item, marcar o checkbox antes de seguir pro próximo. Na seção
**Features**, não pular a ordem — cada uma depende da anterior conforme
a coluna "Depende de". Ao priorizar um item de Bug/Débito/Compliance,
ele sai da lista de pendências e vira trabalho normal (spec nova via
`/specify` ou Modo Leve, conforme o caso — ver critério no `/CLAUDE.md`
raiz).

## Decisões de modelagem já fechadas (contexto para o `/plan` de cada FEAT)

- **DynamoDB single-table, sem migração de dados** — tabela pode ser
  recriada do zero, sem compatibilidade retroativa com o que existe hoje
- **Novo tenant: `Conta`** substitui `USER#<userId>` como partição
  principal de `Category`/`Expense`/futura `Transaction`. Um usuário
  pode pertencer a múltiplas contas (`GSI1 PK=USER#<userId>` já modelado
  pra isso desde já, mesmo que o front hoje só use uma)
- **Sem tabela agregada / sem DynamoDB Streams** — `Resumo` e
  `Relatórios` são sempre `Query` do período + agregação em memória na
  própria request. Ponto de reavaliação: só se algum dia uma conta
  acumular milhares de transações/ano (não é o caso hoje)
- **Comprovante de despesa/receita fica só de UI por enquanto** — sem
  bucket S3, sem `ReceiptS3Key` no modelo, adiado pra feature própria
  futura
- **Orçamento por categoria é um valor mensal recorrente** (atributo
  `OrcamentoMensalCents` na própria `Category`), não versionado por mês
- Modelo completo (item types, PK/SK/GSI) detalhado na conversa do dia
  2026-08-22; será formalizado em `backend/docs/data-model.md` conforme
  cada FEAT abaixo for implementada (mesmo padrão já usado pra FEAT-16)

## Features

- [x] **FEAT-19 — Conta (fundação multi-tenant)**
  Cria `Account` + `Membership` (titular) via trigger `Post
  Confirmation` do Cognito (novo Lambda), assim que o usuário confirma o
  cadastro — com resolução idempotente também no primeiro login como
  rede de segurança (falha do trigger, usuário criado fora do fluxo
  padrão, limitação do `cognito-local`). Migra `Category` e `Expense` de
  `PK=USER#<userId>` para `PK=ACCOUNT#<accountId>`, resolvendo o
  `accountId` a partir do `userId` do JWT em todo request. Contrato das
  rotas existentes não muda — é troca de chave interna, transparente pro
  usuário único de hoje.
  Depende de: FEAT-01 (auth), FEAT-16/17 (categorias/despesas) — já prontas.

- [x] **FEAT-20 — Membros da conta, convites e permissões**
  `GET/POST/DELETE /members`, convite por e-mail (`Status=ConvitePendente`
  → aceite no login), níveis de acesso `Leitura`/`Lançar`/`Total`.
  Aplica autorização por role em todos os endpoints já existentes
  (despesas, categorias).
  Depende de: FEAT-19.

- [x] **FEAT-21 — Categoria: tipo e orçamento**
  Adiciona `tipo` (`despesa`|`receita`, obrigatório) e
  `orcamentoMensalCents` (opcional) a `Category`. `GET /categories`
  ganha filtro `?tipo=`. Editar orçamento exige role `Total`.
  Depende de: FEAT-19 (e idealmente FEAT-20, pra já nascer com a role certa).

- [x] **FEAT-22 — Transações: generalizar Despesa para Receita/Despesa**
  Generaliza `Expense` → `Transação`: renomeia `/expenses` → `/transactions`
  (rota única, filtro `?tipo=`), adiciona `tipo` (`despesa`|`receita`,
  validado contra a categoria referenciada), expõe `createdByUserId`/
  `createdByLabel` (pra "Lançado por: Você"). Papel `Lancar` passa a
  poder editar/excluir só o que criou. Reaproveita a mecânica de chave
  já existente (`TXN#`, GSI1 por categoria, GSI2 por id).
  Depende de: FEAT-19, FEAT-21.

- [x] **FEAT-23 — Resumo mensal (dashboard)**
  `GET /summary?month=YYYY-MM`: saldo, receitas, gastos, orçamento
  total, restante, gasto por categoria, últimos lançamentos — calculado
  via `Query` + agregação em memória, sem tabela agregada.
  Depende de: FEAT-22, FEAT-21.

- [x] **FEAT-24 — Relatórios por período**
  `GET /reports?period=week|month|year`: gasto por categoria, total do
  período, variação vs período anterior, maior gasto. Mesma estratégia
  de cálculo do FEAT-23.
  Depende de: FEAT-22, FEAT-21.

- [x] **FEAT-25 — Exportação CSV de transações** *(menor, pode ficar por
  último ou fora desta leva)*
  `GET /transactions/export` gerando CSV a partir da mesma `Query` de
  transações do período — cobre o botão "Exportar CSV" de Ajustes.
  Depende de: FEAT-22.

- [x] **FEAT-26 — Perfil do usuário no cadastro (nome, telefone, CPF)**
  *(inserida fora da ordem original desta lista, a pedido do usuário,
  empurrando as duas linhas seguintes uma posição adiante)*
  `POST /auth/register` passa a exigir também `name`, `phoneNumber` e
  `cpf`, armazenados num novo item de perfil no DynamoDB (não em
  atributos do Cognito — CPF não é atributo padrão e um atributo
  customizado só pode ser definido na criação do User Pool). CPF único
  e validado por dígito verificador. `GET /auth/me` passa a expor os
  três campos.
  Depende de: FEAT-01 (auth).

- [ ] **FEAT-27 — E-mail de boas-vindas** *(substituída por FEAT-37,
  abaixo — a leva "Autenticação — área não logada" de 2026-09-01
  formalizou a infra de e-mail que este item deixava em aberto. Mantido
  aqui só como histórico; não implementar isoladamente.)*
  Envia e-mail de boas-vindas quando a conta é criada (mesmo trigger
  `Post Confirmation` da FEAT-19). Exige decidir/provisionar
  infraestrutura de e-mail (SES ou similar) — inexistente no projeto
  hoje. Escopo deixado de fora da FEAT-19 de propósito.
  Depende de: FEAT-19.

- [x] **FEAT-28 — Seed de categorias padrão**
  Cria automaticamente um conjunto de categorias padrão para toda conta
  nova. Já tinha sido adiado na FEAT-16; retomado aqui porque a criação
  de conta (FEAT-19) é o gatilho natural. Implementada fora da ordem
  original desta lista, a pedido do usuário, antes da FEAT-27.
  Depende de: FEAT-19, FEAT-16.

- [x] **FEAT-30 — Categoria: escopar busca por ID (GSI2) por conta**
  *(inserida fora da ordem original desta lista — nasceu de um bug
  encontrado em 2026-08-31 e registrado no backlog, corrigido pelo
  schema em vez de correção rápida)*
  Corrige `GSI2PK` de `Category`, que usava só `ID#<categoryId>` e
  colidia entre contas nas 13 categorias padrão (mesmos ids literais
  hardcoded pela FEAT-28 em toda conta nova). Passa a
  `ID#<accountId>#<categoryId>`, alinhado ao mesmo padrão de escopo por
  conta já usado no resto do modelo. Ver
  `backend/specs/FEAT-30-categoria-gsi2-escopo-conta/`.
  Depende de: FEAT-19, FEAT-28.

## Autenticação — área não logada (2026-09-01)

Sequência combinada em 2026-09-01 a partir da atualização do design
system (`frontend/design-system/web/screenshots/2{0..7}-*.png`,
`frontend/design-system/mobile/screenshots/2{0..7}-*.png` e
`frontend/design-system/emails/`), que passou a assumir confirmação de
cadastro por OTP, recuperação de senha em 3 passos e 4 e-mails
transacionais com HTML próprio (confirmação, recuperação, senha
alterada, boas-vindas). Segue a mesma mecânica das demais linhas deste
arquivo — cada item vira `spec.md` própria via `/specify`.

**Decisões já confirmadas com o usuário (2026-09-01):**
- **E-mail com marca própria desde já, via SES** (não o e-mail padrão,
  sem estilo, do Cognito) — FEAT-33 provisiona a infra antes de
  qualquer um dos outros itens depender dela. Como qualquer recurso AWS
  novo, **exige aprovação explícita do usuário antes do `terraform
  apply`** (ver `backend/infra/CLAUDE.md`, seção de custo/segurança).
- **O timer de 60s do protótipo (`otpSeconds`) vira só um cooldown de
  reenvio no frontend, não uma expiração real de código.** O backend
  usa os fluxos nativos do Cognito (`ConfirmSignUp`/`ForgotPassword`,
  TTL padrão deles) — sem tabela própria de OTP, sem novo mecanismo de
  rate limit/brute force pra manter. Copy dos e-mails ("expira em 1
  minuto") deve ser revisada nas specs de FEAT-35/36 pra não prometer
  algo que o backend não cumpre.
- **Motivação extra pra sair do e-mail padrão do Cognito:** o teto de
  50 e-mails/dia (sem SES) já é a razão documentada de
  `GastosApp.IntegrationTests` ter sido tirado dos pipelines de
  hom/prod em 2026-09-01 (ver `backend/infra/CLAUDE.md`). FEAT-33
  resolve isso como efeito colateral — reavaliar naquele momento se dá
  pra devolver os testes integrados aos workflows.

- [x] **FEAT-33 — Infraestrutura de e-mail transacional (SES)** *(concluída,
  ver `backend/specs/FEAT-33-infra-email-transacional-ses/`)*: SES
  provisionado em hom e prod (identidade de domínio + DKIM verificados,
  `ses.tf` por ambiente), Cognito de ambos os ambientes enviando via
  SES (`email_configuration`), IAM `ses:SendEmail`/`ses:SendRawEmail`
  concedido às Lambdas da API principal e do trigger de conta. Pedido
  de saída do sandbox do SES enviado (`ReviewDetails.Status =
  PENDING` na conclusão desta feature — conferir status atual antes de
  assumir que já saiu, ver `backend/infra/CLAUDE.md`). Fluxo de
  cadastro/login validado em hom sem regressão (e-mail de confirmação
  chegou, mas caiu no spam — ver débito abaixo).

- [x] **FEAT-34 — Custom Message trigger do Cognito (e-mails de auth com HTML)**
  Novo handler em `GastosApp.CognitoTriggers` (ao lado do
  `AccountTriggerHandler` já existente) pro trigger `CustomMessage` do
  Cognito, cobrindo os `TriggerSource` `CustomMessage_SignUp`,
  `CustomMessage_ResendCode` e `CustomMessage_ForgotPassword`: troca o
  corpo padrão pelo HTML de `frontend/design-system/emails/
  01-confirmacao-cadastro.html` (cadastro/reenvio) e
  `02-recuperacao-senha.html` (recuperação), substituindo o
  `{{codigo}}` do template pelo `codeParameter` que o próprio evento
  do Cognito já injeta. O envio em si continua sendo feito pelo
  Cognito (via SES configurado na FEAT-33) — este trigger só formata,
  não chama `ses:SendEmail` diretamente.
  Depende de: FEAT-33.

- [x] **FEAT-35 — Confirmação de cadastro via código (OTP)** *(concluída,
  ver `backend/specs/FEAT-35-confirmacao-cadastro-otp/`)*:
  `POST /auth/confirm` (`ConfirmSignUpAsync`) e `POST /auth/resend-
  confirmation` (`ResendConfirmationCodeAsync`). Novos erros em
  `AuthErrors` (`invalid-confirmation-code` ← `CodeMismatchException`/
  `UserNotFoundException`, `expired-confirmation-code` ←
  `ExpiredCodeException`) — reaproveita `AuthErrors.UserNotConfirmed`,
  já usado pelo login (`CognitoAuthService.LoginAsync`) desde antes
  desta leva. Nenhuma mudança de TTL no Cognito (ver decisão acima).
  489 unit + 214 componente + 30 integrado passando (3 integrados
  pulados em modo Local por limitação do `cognito-local`, ver débito
  técnico abaixo — pendente validação real em hom via
  `backend-integration-tests-hom.yml`).

- [ ] **FEAT-36 — Recuperação de senha (esqueci minha senha)**
  `POST /auth/forgot-password` (`ForgotPasswordAsync`) e `POST /auth/
  reset-password` (`ConfirmForgotPasswordAsync`), com erros
  `invalid-reset-code`/`expired-reset-code` (`CodeMismatchException`/
  `ExpiredCodeException`) e reaproveitando `AuthErrors.Validation` pra
  senha fora da política (`InvalidPasswordException` — política real do
  Cognito exige maiúscula+minúscula+número+símbolo, não só "mín. 8
  caracteres" como o texto do protótipo sugere; frontend precisa
  espelhar isso, ver backlog do frontend). Após
  `ConfirmForgotPasswordAsync` bem-sucedido, dispara na hora o e-mail
  de "senha alterada" (`03-senha-alterada.html`) via `ses:SendEmail`
  direto do backend (não passa pelo Custom Message trigger — não é
  código do Cognito) com `{{data}}` da própria request;
  `{{dispositivo}}` pode ficar com o `User-Agent` cru como fallback (sem
  parsing de dispositivo no projeto hoje — refinar como débito futuro
  se o usuário quiser).
  Depende de: FEAT-33 (SES). Não depende de FEAT-34/35.

- [ ] **FEAT-37 — E-mail de boas-vindas** *(substitui a antiga FEAT-27
  deste arquivo)*
  O trigger `Post Confirmation` já existente (`AccountTriggerHandler`,
  FEAT-19) passa a também enviar `04-boas-vindas.html` via
  `ses:SendEmail` direto (mesmo padrão da FEAT-36), depois de
  `EnsureAccountCommand` ter sucesso — precisa acrescentar
  `ses:SendEmail` na IAM role já existente do Lambda de trigger
  (`lambda-account-trigger.tf`). Falha no envio do e-mail não pode
  bloquear a criação da conta (mesma filosofia defensiva já aplicada ao
  `EnsureAccountCommand` no `AccountTriggerHandler` — só loga).
  Depende de: FEAT-33, FEAT-19 (já pronto).

## Bugs

- [x] **BUG — Login não exige perfil completo quando o usuário é criado
  diretamente no Cognito** (levantado em 2026-08-31, fora do escopo de
  qualquer FEAT em andamento) *(resolvido, ver
  `backend/specs/FEAT-31-login-perfil-incompleto/`)*: o fluxo normal
  (`POST /auth/register`, FEAT-26) exige `name`, `phoneNumber` e `cpf`
  antes de criar o perfil no DynamoDB. Mas se um administrador cadastra
  o usuário proativamente no Cognito (fora do `/auth/register`) e já
  confirma o acesso, `LoginUserCommandHandler` autenticava via
  `IAuthService.LoginAsync` sem checar se existe perfil com os campos
  obrigatórios preenchidos — o usuário logava normalmente mesmo sem
  nome/CPF/telefone cadastrados. FEAT-31 bloqueou o login (403
  `profile-incomplete`) nesse caso.

## Débitos técnicos e melhorias futuras

Itens levantados durante specify/plan/tasks/implementação/review ou
Modo Leve, fora do escopo do que estava sendo feito no momento — ver
"Débitos técnicos e oportunidades de melhoria" no `/CLAUDE.md` raiz do
monorepo.

- [x] **DÉBITO — Módulos sem teste integrado ainda** (levantado na
  FEAT-29 — `backend/specs/FEAT-29-testes-integrados/`) *(resolvido,
  ver `backend/specs/FEAT-32-testes-integrados-modulos-pendentes/`)*:
  a infraestrutura de testes integrados (suíte multiambiente, execução
  local via Docker/Native AOT/Runtime Interface Emulator, gates de
  CI/CD em hom/prod) foi entregue cobrindo só o módulo Auth como prova
  de conceito — os demais módulos existentes continuavam sem teste
  integrado, cobertos só por teste de componente (mocks). FEAT-32
  preencheu os 7 módulos, seguindo o padrão já estabelecido
  (`TestAccountFixture` + `<Modulo>/<Modulo>FlowTests.cs`): Categorias
  (`FEAT-16`/`FEAT-21`), Transações (`FEAT-22`), Membros/convites
  (`FEAT-20`), Resumo mensal (`FEAT-23`), Relatórios por período
  (`FEAT-24`), Exportação CSV (`FEAT-25`), Perfil do usuário
  (`FEAT-26`).

- [ ] **DÉBITO — `DELETE /members` remove o membro em vez de
  inativá-lo** (confirmado com o usuário durante a FEAT-22): deveria
  bloquear a remoção de um membro que já lançou transações,
  transformando-o em `Inativo` (novo `Status` de `Membership`) em vez
  de removê-lo de fato — um membro `Inativo` continuaria aparecendo
  como `createdByLabel` nas transações que já criou. Hoje
  (FEAT-20/FEAT-22) `DELETE /members` remove o `Membership`
  incondicionalmente; transações de um membro removido caem no
  fallback `createdByLabel="Ex-membro"` (ver
  `backend/specs/FEAT-22-transacoes-receita-despesa/`).

- [ ] **DÉBITO — `backend-feature-pr.yml` não dispara para mudanças só
  em `backend/infra/terraform/**`** (percebido num fix pontual pós
  FEAT-32, PR #86): o filtro `paths` do workflow cobre
  `backend/src/**`, `backend/tests/**`, `backend/infra/lambda/**`,
  `backend/GastosApp.sln` e `.github/workflows/backend-*.yml`, mas não
  `backend/infra/terraform/**` — uma branch `fix/*`/`FEAT-*` que só
  altera Terraform (ex.: ajuste de IAM policy da role de CI/CD) nunca
  abre PR automático pra `develop`, exigindo `gh pr create` manual.
  Decidir se `backend/infra/terraform/**` deve entrar nesse filtro
  (ou se mudanças de Terraform devem mesmo ficar fora do PR
  automático, por não passarem pelo gate de build/teste de código) —
  ver `backend/infra/CLAUDE.md`.

- [ ] **DÉBITO — E-mail via SES cai em spam (falta SPF/MAIL FROM
  customizado e DMARC)** (percebido na validação manual da FEAT-33 —
  `backend/specs/FEAT-33-infra-email-transacional-ses/`): a identidade
  de domínio SES de hom/prod só tem DKIM habilitado (escopo fechado da
  spec); o e-mail de confirmação de cadastro chegou ao destinatário de
  teste (Gmail), mas na caixa de spam. Causa provável: sem um domínio
  MAIL FROM customizado (registro SPF apontando pro SES) nem um
  registro DMARC para `jrnexpenses.com`, a autenticação do remetente
  fica incompleta aos olhos de provedores como o Gmail, mesmo com DKIM
  válido. Resolver antes de qualquer e-mail novo ir pra produção com
  volume real (FEAT-34/36/37) — senão o problema se repete em todos
  eles. Passos possíveis: `aws_ses_domain_mail_from` (subdomínio tipo
  `mail.jrnexpenses.com`, mais um record MX e um TXT/SPF na hosted
  zone) e um record TXT de DMARC (`_dmarc.jrnexpenses.com`, política
  inicial `p=none` pra só monitorar antes de enforçar).

- [ ] **MELHORIA — Revisar throttling/brute-force do código de reset de
  senha na FEAT-36** (levantado no `/specify` da FEAT-35 —
  `backend/specs/FEAT-35-confirmacao-cadastro-otp/`): o código do
  Cognito (`ConfirmSignUp`/`ConfirmForgotPassword`) é sempre válido por
  24h, fixo e não configurável (nem via console/API, nem via Terraform
  — confirmado na documentação oficial da AWS). Pra `POST /auth/confirm`
  (FEAT-35) isso é aceitável: possuir o código não dá acesso à conta,
  só confirma o email — login continua exigindo a senha, nunca enviada
  por email. Já pra `POST /auth/reset-password` (FEAT-36,
  `ConfirmForgotPassword`), o cálculo muda: o código certo permite
  definir uma senha nova diretamente, ou seja, é takeover de conta
  completo. Existe pesquisa de segurança pública (Pentagrid, 2021)
  documentando que o throttling do Cognito pra esse fluxo específico já
  foi, na prática, bem mais fraco que o anunciado ("5 a 20
  tentativas/hora") — até ~1.587 tentativas antes de bloquear, com o
  código permanecendo válido mesmo após "limite excedido" — vulnerabi-
  lidade corrigida pela AWS em abril/2021, sem recorrência pública
  conhecida desde então. Retomar essa discussão ao especificar a
  FEAT-36: vale medir/validar o throttling real do Cognito antes de
  assumir que a proteção nativa é suficiente para um fluxo que troca
  senha.

- [ ] **MELHORIA — `terraform apply` via esteira de CI/CD** (levantado
  no `/plan` da FEAT-34 —
  `backend/specs/FEAT-34-custom-message-emails-auth/`): hoje todo
  `terraform apply` é manual, rodado localmente por alguém com
  credenciais AWS de fato — a esteira (`backend-deploy-*.yml`) só
  publica código (`aws lambda update-function-code`/
  `update-function-configuration`), nunca toca infraestrutura. A ideia
  de automatizar o `apply` via GitHub Actions é tecnicamente viável (o
  state dos ambientes já é remoto — S3 + `use_lockfile`, seguro de
  rodar de qualquer máquina), mas exige desenho próprio antes de valer
  a pena: a role `gastosapp-backend-cicd` precisaria ganhar permissões
  bem mais amplas que as de hoje (`iam:CreateRole`/`PutRolePolicy`,
  `lambda:CreateFunction`/`AddPermission`, `cognito-idp:UpdateUserPool`,
  `logs:CreateLogGroup` etc. — dependendo de quais `.tf` o workflow
  puder tocar), e um gate de aprovação manual antes do `apply` rodar de
  fato (auto-apply a cada push seria arriscado pra infra, diferente de
  deploy de código). Cross-cutting — beneficiaria qualquer feature
  futura que precise provisionar recurso novo, não só a FEAT-34, que
  seguiu com `apply` manual (decisão confirmada com o usuário).

- [ ] **DÉBITO — `cognito-local` (v5.3.0, última versão publicada) não
  reproduz 3 comportamentos do Cognito real usados por `ConfirmSignUp`/
  `ResendConfirmationCode`** (descoberto na FEAT-35 —
  `backend/specs/FEAT-35-confirmacao-cadastro-otp/`, rodando
  `run-local.sh`): (1) `ResendConfirmationCode` não existe entre os
  targets implementados do pacote (verificado inspecionando
  `/usr/local/lib/node_modules/cognito-local/lib/targets/` dentro do
  container) — a chamada ao SDK propaga como exceção não mapeada (500);
  (2) `ConfirmSignUp` (`lib/targets/confirmSignUp.js`) nunca checa
  `UserStatus`, só compara o `ConfirmationCode` salvo — o branch de
  idempotência do Cognito real (`NotAuthorizedException` pra usuário já
  `CONFIRMED`, que no real dispara antes de olhar o código) é
  inalcançável contra o emulador, e `AdminConfirmSignUp` também não
  limpa o `ConfirmationCode` salvo; (3) pra usuário inexistente,
  `ConfirmSignUp` lança `NotAuthorizedError` (não `UserNotFoundError`,
  como o Cognito real e a documentação do AWS SDK) — nosso catch de
  "já confirmado" acaba absorvendo isso como sucesso. Não há correção
  via upgrade: v5.3.0 já é a última release oficial (conferido via
  GitHub API); existe um PR de terceiro (#468, "100% Cognito API
  parity") mas está aberto, não mergeado, não publicado. Os 3 testes
  afetados (`Confirm_UsuarioJaConfirmado_Retorna200Idempotente`,
  `Confirm_EmailInexistente_Retorna400`, `ResendConfirmation_
  UsuarioNaoConfirmado_Retorna200` em
  `backend/tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs`)
  pulam a asserção em modo Local (guarda `IntegrationTestEnvironment.
  Current.IsLocal`) e só validam de verdade contra Cognito real via
  `backend-integration-tests-hom.yml`. Reavaliar se/quando o pacote
  ganhar uma versão nova que cubra esses 3 casos, ou se outra feature
  de auth precisar de mais paridade do emulador.

## Compliance (LGPD)

Levantado durante o `/specify` da FEAT-26 (coleta de CPF no cadastro).
Sem timeline — só entram na seção **Features** quando o usuário decidir
priorizar, ex.: se o projeto deixar de ser uso pessoal.

- [ ] **LGPD — Direito de exclusão/anonimização** (`Art. 16` LGPD): hoje
  não existe fluxo de encerramento de conta; ao existir, precisa apagar
  ou anonimizar nome/telefone/CPF, não reter indefinidamente.

- [ ] **LGPD — Direito de retificação** (`Art. 18` LGPD): edição do
  próprio perfil (nome/telefone/CPF) pelo usuário — hoje fora do escopo
  da FEAT-26 de propósito.

- [ ] **LGPD — Base legal e consentimento explícito** (`Art. 7º`/`Art.
  9º` LGPD): tela de cadastro sem Termos de Uso/Política de Privacidade
  hoje — necessário formalizar a finalidade da coleta de CPF antes de
  qualquer uso além de identificação da conta.

- [ ] **LGPD — Transferência internacional de dados** (`Art. 33` LGPD):
  infra de produção roda em `us-east-1`
  (`backend/infra/terraform/environments/prod/variables.tf`) —
  reavaliar migração para `sa-east-1` (São Paulo) se o volume de dados
  pessoais justificar simplificar essa exigência.

- [ ] **LGPD — Encarregado (DPO) e plano de resposta a incidente**: não
  obrigatório no porte atual, mas necessário antes de qualquer escala
  maior, dado que CPF é alvo preferencial de fraude.

## Fora desta leva, de propósito

Itens deliberadamente fora de escopo — não são pendências, não entram
como checkbox. Só migram pra uma das seções acima se houver decisão
explícita de priorizar.

- Comprovante/anexo real (upload S3)
- Agregação materializada / DynamoDB Streams
- Ajustes → notificações push/e-mail reais e seletor de moeda (hoje sem
  infra nenhuma por trás — ficam só de UI no front até haver decisão
  explícita de criar essa infra)
