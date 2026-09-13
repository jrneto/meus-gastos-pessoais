# Terraform — backend/infra

Provisiona a infraestrutura AWS do backend. **Desde a FEAT-40 (etapas
6 e 7), `environments/{hom,prod}/` só gerenciam o workload** — as 3
Lambdas .NET Native AOT (API + os 2 triggers do Cognito, `lambda*.tf`),
suas roles/policies/log groups/permissions; a plataforma (tabela
DynamoDB, Cognito User Pool + App Client, Parameter Store, SES, API
Gateway, domínio customizado + ACM/DNS) foi movida para o repositório
[`infra-jrnexpenses`](https://github.com/jrneto/infra-jrnexpenses) e é
lida aqui via `terraform_remote_state` (`remote_state.tf`), tanto em
hom quanto em prod — mesmo descrito historicamente em
`backend/specs/FEAT-09-terraform-cognito-parameter-store/` e
`backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/`.

Duas configurações independentes neste diretório (`bootstrap/` e
`cicd/` migraram para o repositório `infra-jrnexpenses` na FEAT-40,
etapa 5 — ver pointers abaixo):

- `bootstrap/` — **migrado para
  [`infra-jrnexpenses/terraform/bootstrap/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/bootstrap)**
  (FEAT-40, etapa 5). Continua criando o bucket S3 que guarda o state
  remoto de todas as configurações do projeto (as deste diretório e as
  do repositório novo), com **próprio state local** — passo a passo
  no README de lá.
- `environments/prod/` — ambiente de **produção**
  (`api.jrnexpenses.com`), state próprio no bucket criado pelo
  `bootstrap/` (`key = gastosapp/prod/terraform.tfstate`), usando o
  locking nativo do backend S3 (`use_lockfile`). Desde a FEAT-40 (etapa
  7), contém só as 3 Lambdas de workload — a plataforma vive em
  [`infra-jrnexpenses/terraform/environments/prod/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/environments/prod)
  (lida via `remote_state.tf`).
- `environments/hom/` — ambiente de **homologação**
  (`api-hom.jrnexpenses.com`, FEAT-13), state próprio no mesmo bucket
  (`key = gastosapp/hom/terraform.tfstate`). Desde a FEAT-40 (etapa 6),
  contém só as 3 Lambdas de workload — a plataforma vive em
  [`infra-jrnexpenses/terraform/environments/hom/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/environments/hom)
  (lida via `remote_state.tf`).
- `cicd/` — **migrado para
  [`infra-jrnexpenses/terraform/cicd/backend/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/cicd/backend)**
  (FEAT-40, etapa 5) — ver seção dedicada abaixo.

Essa organização por ambiente replica o padrão já adotado pelo
Terraform do frontend (`frontend/infra/terraform/environments/prod/`).

## Pré-requisitos

- Terraform >= 1.10 instalado localmente
- AWS CLI configurado com credenciais válidas (profile `default`,
  região `us-east-1` — mesmo padrão usado pelo backend .NET em
  desenvolvimento local, ver `backend/docs/architecture.md`)
- Permissão na conta AWS para criar bucket S3 e os recursos do ambiente
  desejado

## Passo a passo (primeira vez, a partir da sua máquina local)

### 1. Criar o bucket de state (bootstrap)

```bash
cd backend/infra/terraform/bootstrap
terraform init
terraform apply
```

Confirme a criação (`yes`). Ao final, anote o valor do output
`bucket_name` (algo como `gastosapp-terraform-state-123456789012`).

### 2. Inicializar o ambiente desejado apontando para esse bucket

```bash
cd ../environments/prod   # ou ../environments/hom
terraform init \
  -backend-config="bucket=<bucket_name do passo 1>" \
  -backend-config="region=us-east-1"
```

A `key` do state (`gastosapp/prod/terraform.tfstate` ou
`gastosapp/hom/terraform.tfstate`) já vem fixa em `versions.tf` de cada
ambiente — não precisa passar via `-backend-config`.

O Terraform vai perguntar se quer copiar o state existente para o novo
backend — como é a primeira vez, não há state anterior a migrar, apenas
confirme.

### 3. Provisionar os recursos

```bash
terraform plan
terraform apply
```

Confirme (`yes`). A partir daqui, o state fica no S3 (com locking nativo
via `use_lockfile`), então é seguro rodar `terraform plan`/`apply` de
qualquer máquina que tenha as credenciais AWS configuradas — não é mais
um artefato só local.

## Execuções seguintes

Já com o backend configurado, para qualquer um dos ambientes:

```bash
cd backend/infra/terraform/environments/prod   # ou environments/hom
terraform init   # se ainda não rodou nesta máquina
terraform plan
terraform apply
```

Não é necessário repetir o `bootstrap/` — ele só roda de novo se o
bucket de state precisar ser recriado.

## Convenções

- Nenhum novo recurso Terraform deve ser criado sem pedido explícito do
  usuário (ver `backend/infra/CLAUDE.md`).
- Cognito (`backend-cognito.tf`) e Parameter Store
  (`backend-parameter-store.tf`) são gerenciados por Terraform desde a
  FEAT-09, hoje em
  [`infra-jrnexpenses/terraform/environments/{hom,prod}/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform)
  (FEAT-40). Em produção, o User Pool/App Client atuais foram
  **recriados** (não importados) — o pool anterior, criado
  manualmente, foi mantido intacto até exclusão manual pelo usuário. Os
  3 parâmetros do Parameter Store de produção foram trazidos via
  `terraform import` (recurso simples, sem risco de dado) — histórico
  preservado na migração (`state mv`, sem novo `import`). Em
  homologação (FEAT-13), todos os recursos foram criados do zero via
  Terraform, sem import.

## Domínio customizado da API (FEAT-12, migrado na FEAT-40)

Além da URL padrão do API Gateway, a API de produção responde em
`https://api.jrnexpenses.com`. Desde a FEAT-40 (etapa 7), isso é
gerido em
[`infra-jrnexpenses/terraform/environments/prod/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/environments/prod)
pelos arquivos `backend-acm.tf` (certificado ACM, já `ISSUED`,
importado — sem `aws_acm_certificate_validation`, diferente de hom),
`backend-api-gateway-domain.tf` (`aws_apigatewayv2_domain_name` +
`aws_apigatewayv2_api_mapping`) e `backend-dns.tf` (records Route 53).
Os 5 recursos originais já existiam manualmente na conta e foram
trazidos via `terraform import` na FEAT-12 — nenhum recurso novo foi
criado nem por aquela feature nem pela migração da FEAT-40.

A hosted zone `jrnexpenses.com.` vive em
`infra-jrnexpenses/terraform/dns/` (migrada do frontend na FEAT-34).
`backend-dns.tf` (de cada ambiente, no repositório novo) só lê essa
zona por nome (`data "aws_route53_zone"`), sem duplicá-la ou geri-la,
para poder gerenciar os records de `api.jrnexpenses.com` (prod) ou
`api-hom.jrnexpenses.com` (hom) dentro dela. Ver
`backend/specs/FEAT-12-terraform-dominio-customizado-api/` (contexto
original) e `backend/specs/FEAT-40-extracao-infra-repo-apartado/`
(migração).

## Deploy da Lambda (FEAT-10)

A API .NET roda como Lambda Native AOT (runtime customizado
`provided.al2023`), atrás de um API Gateway HTTP API — sem autorizador
JWT no Gateway, autenticação continua só na aplicação (FEAT-01).

Build e empacotamento (`infra/lambda/Dockerfile.build` +
`infra/lambda/build.sh`) rodam num container **Amazon Linux 2023** (a
mesma base do runtime da Lambda — necessário para compatibilidade de
glibc; a imagem oficial do SDK .NET, baseada em Ubuntu, gera um binário
que não roda na Lambda). O script gera `infra/lambda/function.zip`, que
o `lambda.tf` de **cada ambiente** referencia via `filename`/
`source_code_hash` (`${path.module}/../../../lambda/function.zip`) — é
o **mesmo artefato físico** para produção e homologação, já que os dois
ambientes rodam exatamente o mesmo código/contrato (ver FEAT-13). Não
há processo de build separado por ambiente; rodar `apply` em cada
ambiente publica o zip que estiver em disco no momento, então não há
garantia automática de que produção e homologação estejam sempre no
mesmo código a menos que se aplique o mesmo zip nos dois (aceitável
enquanto o deploy for manual — ver seção seguinte).

**Desde a FEAT-14, esse fluxo é automatizado via GitHub Actions** (ver
seção "`cicd/`" abaixo) — os workflows publicam o zip direto na Lambda
via `aws lambda update-function-code` (sem rodar `terraform apply`).
O fluxo manual abaixo continua útil para desenvolvimento local ou
qualquer situação fora do fluxo automatizado:

```bash
cd backend
bash infra/lambda/build.sh   # gera infra/lambda/function.zip
cd infra/terraform/environments/prod   # ou environments/hom
terraform plan
terraform apply
```

Toda vez que o código da API mudar, repita esse fluxo para cada
ambiente que precisar do deploy — o `source_code_hash` no `lambda.tf`
muda junto com o zip, e o `terraform plan` mostra a atualização do
código como a única mudança.

## Ambiente de homologação (FEAT-13, workload movido na FEAT-40)

`environments/hom/` expõe a API em `https://api-hom.jrnexpenses.com`.
Desde a FEAT-40 (etapa 6), este diretório só contém o **workload**: as
3 Lambdas (`lambda.tf`, `lambda-account-trigger.tf`,
`lambda-custom-message-trigger.tf`), suas roles/policies/log
groups/permissions e `remote_state.tf` (lê a plataforma via
`terraform_remote_state`). A **plataforma** — tabela DynamoDB
(`GastosApp-Hom`), Cognito User Pool + App Client
(`user-pool-gastos-app-hom`, `controle-gastos-spa-hom`), Parameter
Store (`/GastosApp/Hom/...`), SES e o domínio customizado
(`api-hom.jrnexpenses.com`, ACM, DNS) — vive em
[`infra-jrnexpenses/terraform/environments/hom/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/environments/hom)
(`backend-*.tf` de lá).

Pontos que continuam relevantes para quem mexe no workload deste
diretório:

- `ParameterStore__Path=/GastosApp/Hom/` (variável de ambiente da
  Lambda da API) isola a leitura do Parameter Store no prefixo de
  homologação — sobrepõe o default `/GastosApp/` lido em produção
  (`AwsParameterStoreExtensions.cs`/`Program.cs`, sem alterar contrato
  de API)
- `DynamoDb__TableName` (Lambda da API e do trigger de conta) vem de
  `local.dynamodb_table_name` (`remote_state.tf`), não mais de um
  recurso local — o valor resolvido continua `GastosApp-Hom`. O
  binding de `DynamoDbOptions` usa leitura manual, não
  `services.Configure<T>(IConfiguration)` (reflection, **falha
  silenciosamente sob Native AOT** — achado da FEAT-13, ver
  `backend/infra/CLAUDE.md`)
- Lambda (`gastos-app-api-hom`) e API Gateway HTTP API
  (`gastos-app-api-hom`, agora do lado da infra) seguem com o mesmo
  artefato de produção
- CORS (`frontend_origins`, variável da infra) aponta para
  `https://hom.jrnexpenses.com` desde a FEAT-11/FEAT-34 do frontend —
  `callback_urls` do Cognito ainda usa o placeholder
  `http://localhost:5173` (débito registrado em
  `backend/infra/CLAUDE.md`)

Ver `backend/specs/FEAT-13-ambiente-homologacao/` (contexto original) e
`backend/specs/FEAT-40-extracao-infra-repo-apartado/` (migração da
plataforma) para spec e plano técnico completos.

## `cicd/` — OIDC Provider (reaproveitado) + IAM Role do backend (FEAT-14, migrado na FEAT-40)

**Migrado para
[`infra-jrnexpenses/terraform/cicd/backend/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/cicd/backend)**
(FEAT-40, etapa 5) — mesmos arquivos, sem alteração de conteúdo,
`key` do state (`gastosapp-backend/cicd/terraform.tfstate`) mantida
dormente. Continua fora de qualquer state (guardrail de IAM do perfil
usado para aplicar Terraform, ver `README.md`/`CLAUDE.md` do
repositório novo): a IAM Role `gastosapp-backend-cicd` e a policy
inline foram criadas manualmente no console AWS, com o JSON gerado a
partir de `iam-role.tf`/`iam-policy.tf`; o README de lá documenta o
JSON de referência (com um débito conhecido de estar desatualizado,
não corrigido nesta migração — ver `backend/docs/backlog.md`) e o
passo a passo de `import`, se a permissão de leitura de IAM for
liberada no futuro.

**Uso pelos workflows**: o ARN da Role continua cadastrado como
variável `CICD_ROLE_ARN` nos GitHub Environments
`backend-hom`/`backend-prod` (neste monorepo) — não depende de state
Terraform para funcionar, só do recurso existir de fato na conta;
nenhum workflow foi alterado por esta migração.

**Convenção de tag `backend-v*`**: como o repositório é compartilhado
com o frontend (que usa `vX.Y.Z`), as releases do backend usam o
prefixo `backend-v` — necessário pros workflows de deploy de produção e
de rascunho automático de release não se atropelarem entre os dois
contextos (ver `backend/specs/FEAT-14-cicd-github-actions/plan.md`,
decisão 5).
