# FEAT-33: Infraestrutura de e-mail transacional (SES)

## Objetivo

Provisionar Amazon SES para hom e prod como base de envio de e-mail com
marca própria do GastosApp, substituindo o envio padrão (sem estilo,
sem domínio próprio) do Cognito. Esta feature só provisiona a
infraestrutura de envio — nenhum e-mail novo é efetivamente disparado
por ela; isso é escopo das features que dependem dela (FEAT-34, 36, 37).

## Contexto

A atualização do design system em 2026-09-01
(`frontend/design-system/web/screenshots/2{0..7}-*.png`,
`frontend/design-system/mobile/screenshots/2{0..7}-*.png` e
`frontend/design-system/emails/`) passou a assumir confirmação de
cadastro por código (OTP), recuperação de senha em 3 passos e 4
e-mails transacionais com HTML próprio (confirmação de cadastro,
recuperação de senha, senha alterada, boas-vindas — ver
`frontend/design-system/emails/README.md`).

Hoje o Cognito envia esses e-mails (cadastro, recuperação de senha) com
o remetente e template padrão dele, sem HTML customizado e sem domínio
próprio — e com um teto de 50 e-mails/dia por conta AWS (compartilhado
entre hom e prod), já responsável por `GastosApp.IntegrationTests` ter
sido tirado dos pipelines de hom/prod em 2026-09-01 (~35 `SignUp` por
execução da suíte, ver `backend/infra/CLAUDE.md`). Decisão já fechada
com o usuário: sair desse envio padrão para um remetente com marca
própria via SES é pré-requisito de qualquer um dos e-mails novos —
nenhum deles pode depender do envio padrão do Cognito.

**Domínio**: os templates HTML usam `jrnexpenses.com.br` nos links e no
rodapé, mas isso é placeholder — o próprio
`frontend/design-system/emails/README.md` instrui "trocar as URLs
app.jrnexpenses.com.br pelos links reais antes de enviar". O domínio
real, já registrado e sob Terraform (hosted zone `jrnexpenses.com.`,
gerenciada pelo Terraform do **frontend**,
`frontend/infra/terraform/dns/`, referenciada por `backend/infra/
terraform/` só como leitura, ver `backend/specs/
FEAT-12-terraform-dominio-customizado-api/spec.md`), é `jrnexpenses.com`
(sem `.br`) — mesmo domínio já usado por `api.jrnexpenses.com` e pelo
frontend. É esse domínio real que esta feature verifica no SES; a
correção dos links `.com.br` nos templates fica com quem consumir cada
template (FEAT-34/36/37), não é escopo daqui.

**Separação hom/prod**: hom e prod rodam na mesma conta e região AWS
(`648443184523`, `us-east-1`), cada um com seu próprio state Terraform
(`backend/infra/terraform/environments/{prod,hom}/`). Verificar o mesmo
domínio raiz duas vezes, em dois states diferentes, geraria conflito de
propriedade do recurso. Decisão fechada com o usuário: cada ambiente
verifica sua própria identidade de e-mail, dona do seu próprio state —
prod verifica o domínio raiz `jrnexpenses.com`, hom verifica o
subdomínio `hom.jrnexpenses.com` — mesmo padrão de separação por
subdomínio já usado por `api.jrnexpenses.com`/`api-hom.jrnexpenses.com`
(backend) e `jrnexpenses.com`/`hom.jrnexpenses.com` (frontend).

**Sandbox do SES**: uma conta AWS nova começa no sandbox do SES, que só
permite enviar para destinatários com endereço individualmente
verificado — bloquearia teste com contas de usuário reais/aleatórias
(inclusive `GastosApp.IntegrationTests`, que cria usuários novos a cada
execução). Esta feature precisa investigar e documentar se a conta já
saiu do sandbox; se não, solicitar a saída (aumento de quota, gratuito,
mas sujeito a análise da AWS) é prova de aceite desta feature — sem
sair do sandbox, os e-mails de teste com usuários reais continuam
bloqueados mesmo com a infra pronta.

Precedente direto de estilo/abordagem para infra provisionada do zero
(não import): `backend/specs/FEAT-09-terraform-cognito-parameter-store/`
e `backend/specs/FEAT-13-ambiente-homologacao/`. Precedente de
separação de recurso por subdomínio entre ambientes/contextos:
`backend/specs/FEAT-12-terraform-dominio-customizado-api/spec.md`.

## Requisitos de negócio / restrições

- **Identidade de domínio verificada no SES**, uma por ambiente:
  `jrnexpenses.com` em prod, `hom.jrnexpenses.com` em hom — cada uma
  gerenciada pelo Terraform do próprio ambiente
  (`backend/infra/terraform/environments/{prod,hom}/`), sem duplicar
  nem depender do state do outro ambiente.
- **DKIM habilitado** na identidade de domínio (autenticação de envio;
  reduz a chance de cair em spam) — os registros DNS necessários
  (verificação de domínio + DKIM) são criados na hosted zone
  `jrnexpenses.com.` (gerenciada pelo Terraform do frontend), pelo
  mesmo mecanismo de leitura cross-contexto já usado pela
  FEAT-12 (nunca duplicando a zona).
- **Cognito User Pool passa a enviar e-mail via SES** (`email_
  configuration` do `aws_cognito_user_pool`, hoje ausente — usa o envio
  padrão do Cognito), um por ambiente, apontando pra identidade
  verificada do próprio ambiente.
- **Permissão IAM de envio** (`ses:SendEmail`/`ses:SendRawEmail`)
  concedida às funções Lambda do backend que precisarão enviar e-mail
  diretamente (fora do envio nativo do Cognito): a Lambda da API
  principal (e-mail de "senha alterada", FEAT-36) e a Lambda de trigger
  de conta (e-mail de boas-vindas, FEAT-37) — escopada só à identidade
  de domínio do próprio ambiente, sem permissão ampla (`Resource: "*"`).
- **Sandbox do SES investigado e documentado**: se a conta ainda
  estiver no sandbox, a saída (aumento de quota) precisa ser solicitada
  como parte desta feature — sem isso, e-mail para endereço de usuário
  real (não pré-verificado manualmente) continua bloqueado mesmo com o
  resto da infra pronta.
- **Custo**: SES não tem taxa fixa mensal — cobra só por e-mail
  enviado, e o volume de uma conta pessoal (poucas dezenas de e-mails
  transacionais por dia) fica dentro do free tier ou próximo de custo
  zero. Ainda assim, qualquer `terraform apply` desta feature exige
  aprovação explícita do usuário antes da execução (recurso AWS novo,
  ver `/CLAUDE.md` raiz e `backend/infra/CLAUDE.md`).
- **Nenhuma mudança de comportamento observável de API** — nenhum
  endpoint novo, nenhum contrato alterado. O único efeito observável
  desta feature isolada é o Cognito passar a enviar os e-mails de
  cadastro/recuperação de senha já existentes (texto padrão dele, sem
  HTML customizado ainda — isso é FEAT-34) a partir do remetente do
  domínio verificado, em vez do remetente padrão do Cognito.
- **Nenhuma execução Terraform sem aprovação prévia explícita do
  usuário** — vale tanto para o desenho técnico (`plan.md`) quanto para
  qualquer `terraform apply`/mudança de quota do SES; nada roda de
  forma autônoma.

## User Stories

**US1 — Identidade de e-mail verificada em prod**
- Given o domínio `jrnexpenses.com` já existe e está sob Terraform do
  frontend
- When a infraestrutura desta feature é aplicada em prod
- Then o SES passa a ter uma identidade de domínio verificada para
  `jrnexpenses.com`, com DKIM habilitado, gerenciada pelo Terraform do
  backend (`environments/prod/`)

**US2 — Identidade de e-mail verificada em hom**
- Given o domínio `jrnexpenses.com` já existe e está sob Terraform do
  frontend
- When a infraestrutura desta feature é aplicada em hom
- Then o SES passa a ter uma identidade de domínio verificada para
  `hom.jrnexpenses.com`, com DKIM habilitado, gerenciada pelo Terraform
  do backend (`environments/hom/`), sem conflitar com a identidade de
  prod

**US3 — Cognito envia e-mail via SES**
- Given as identidades de domínio verificadas de US1/US2
- When o Cognito User Pool de cada ambiente é reconfigurado
- Then os e-mails nativos do Cognito (confirmação de cadastro,
  recuperação de senha) passam a ser enviados via SES, a partir de um
  remetente do domínio verificado do próprio ambiente, em vez do envio
  padrão do Cognito

**US4 — Lambdas do backend autorizadas a enviar e-mail**
- Given a Lambda da API principal e a Lambda de trigger de conta já
  existentes em cada ambiente
- When a IAM policy desta feature é aplicada
- Then ambas passam a ter permissão `ses:SendEmail`/`ses:SendRawEmail`,
  escopada à identidade de domínio do próprio ambiente — sem que
  nenhuma outra função Lambda do projeto ganhe essa permissão

**US5 — Status do sandbox do SES investigado e resolvido**
- Given uma conta AWS pode nascer no sandbox do SES (envio restrito a
  destinatários verificados manualmente)
- When esta feature é concluída
- Then o status do sandbox (dentro ou fora) está documentado para
  hom e prod, e — se algum dos dois ainda estiver no sandbox — a
  solicitação de saída já foi enviada à AWS

**US6 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que possa criar, alterar ou destruir um
  recurso AWS (`terraform apply`) ou qualquer solicitação de mudança de
  quota do SES
- When esse comando/solicitação está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nada roda de forma autônoma

**US7 — Nenhuma regressão no fluxo de autenticação existente**
- Given o fluxo de `POST /auth/register` (envia e-mail de verificação
  via Cognito) e `POST /auth/login` já em produção
- When a reconfiguração do `email_configuration` do Cognito é aplicada
- Then o fluxo de cadastro/login continua funcionando exatamente como
  antes — a única diferença observável é o remetente do e-mail

## Critérios de aceite

- [x] `terraform plan`/`state list` de `environments/prod/` mostra uma
      identidade de domínio SES verificada para `jrnexpenses.com`, com
      DKIM habilitado
- [x] `terraform plan`/`state list` de `environments/hom/` mostra uma
      identidade de domínio SES verificada para `hom.jrnexpenses.com`,
      com DKIM habilitado, em state separado do de prod
- [x] Registros DNS de verificação/DKIM de ambas as identidades
      existem na hosted zone `jrnexpenses.com.`, referenciada (não
      duplicada) a partir do Terraform do backend, mesmo padrão de
      cross-referência já usado pela FEAT-12
- [x] `aws_cognito_user_pool` de prod e de hom têm `email_configuration`
      apontando para SES (`email_sending_account = "DEVELOPER"` ou
      equivalente), cada um usando a identidade verificada do próprio
      ambiente
- [x] IAM role da Lambda da API principal e da Lambda de trigger de
      conta, em cada ambiente, têm permissão `ses:SendEmail`/
      `ses:SendRawEmail` escopada à identidade de domínio do próprio
      ambiente (sem `Resource: "*"`, sem conceder a outras Lambdas)
- [x] Status do sandbox do SES documentado para hom e prod em
      `backend/infra/CLAUDE.md`; se algum dos dois estava no sandbox, a
      solicitação de saída foi enviada e seu status (aprovada/pendente)
      está registrado — conta única (`648443184523`) estava no sandbox
      para os dois ambientes; solicitação de saída enviada em
      2026-09-01, status `PENDING` no fechamento desta feature
- [x] `POST /auth/register` seguido de `POST /auth/login` continua
      funcionando sem regressão após a reconfiguração do Cognito,
      validado manualmente em hom (e-mail de verificação chega,
      remetente do domínio `hom.jrnexpenses.com`) — validado com
      identidade individual verificada no SES (necessário por ainda
      estar no sandbox); e-mail chegou, mas caiu no spam (débito
      técnico registrado em `backend/docs/backlog.md`)
- [x] Nenhum `terraform apply` nem solicitação de saída de sandbox
      executado sem aprovação explícita do usuário no momento da
      execução
- [x] `backend/infra/CLAUDE.md` atualizado com a nova seção de SES
      (identidades por ambiente, DKIM, `email_configuration` do
      Cognito, permissões IAM concedidas, status do sandbox)
- [x] `backend/docs/backlog.md` atualizado: FEAT-33 marcada como
      concluída (`- [x]`)

## Status

Concluída em 2026-09-01. Identidades SES verificadas (DKIM habilitado)
em prod (`jrnexpenses.com`) e hom (`hom.jrnexpenses.com`); Cognito de
ambos os ambientes enviando via SES; IAM `ses:SendEmail`/`SendRawEmail`
concedido às Lambdas da API principal e do trigger de conta, escopado
por ambiente. Fluxo de cadastro/login validado manualmente em hom, sem
regressão. Pedido de saída do sandbox do SES enviado — status
`PENDING` no fechamento desta feature (conferir `aws sesv2 get-account`
antes de assumir que já foi aprovado). Achado durante a validação:
e-mail de confirmação caiu no spam (só DKIM configurado, sem SPF/MAIL
FROM customizado/DMARC) — registrado como débito técnico em
`backend/docs/backlog.md`, a resolver antes de qualquer e-mail com
volume real ir pra produção (FEAT-34/36/37).

Achado técnico adicional (não bloqueou a feature, documentado em
`backend/infra/CLAUDE.md`): o guardrail de IAM do perfil
`agent-toolkit`, já conhecido para o OIDC Provider/Role de CI/CD,
também bloqueia leitura (`iam:GetRole`/`GetRolePolicy`) de uma role já
existente e gerenciada pelo Terraform (`jrnexpenses-account-trigger-
lambda-exec`) — os `apply` que tocam IAM/Cognito desta feature
precisaram ser rodados pelo usuário localmente, fora desse perfil.

## Fora do escopo

- Qualquer e-mail com HTML customizado (templates de
  `frontend/design-system/emails/`) — é a FEAT-34 (trigger Custom
  Message do Cognito) e as FEAT-36/37 (e-mails disparados direto do
  backend)
- Corrigir as URLs `app.jrnexpenses.com.br` (placeholder) dentro dos
  templates HTML — responsabilidade de quem consumir cada template
  (FEAT-34/36/37), não desta feature
- Qualquer mudança de contrato de API (nenhum endpoint novo, nenhum
  campo novo em request/response)
- Registrar um domínio novo (`jrnexpenses.com.br` ou qualquer outro) —
  reaproveita o domínio `jrnexpenses.com` já existente, sem custo
  adicional
- Configuration Set do SES com tracking de abertura/clique/bounce
  avançado, ou notificação de bounce/complaint via SNS — pode virar
  débito técnico/melhoria futura se o usuário quiser, mas não é
  pré-requisito das FEAT-34/36/37
- Qualquer mudança na hosted zone `jrnexpenses.com.` em si (criação,
  exclusão) — recurso já gerido pelo Terraform do frontend, fora do
  alcance desta spec; esta feature só acrescenta registros nela
- Pipeline de CI/CD para aplicar Terraform automaticamente — execução
  continua manual, a partir da máquina do usuário, com aprovação
  passo a passo
