# Terraform — backend/infra

Este diretório gerencia só o **workload** do backend: as 3 Lambdas
.NET Native AOT (API + os 2 triggers do Cognito — Post Confirmation e
Custom Message), suas roles/policies/log groups/permissions. Desde a
FEAT-40 (etapas 5-7), toda a **plataforma** — bucket de state
(`bootstrap/`), tabela DynamoDB, Cognito User Pool + App Client,
Parameter Store, SES, API Gateway, domínio customizado (ACM + DNS) e a
referência de CI/CD (`cicd/`) — vive no repositório apartado
[`infra-jrnexpenses`](https://github.com/jrneto/infra-jrnexpenses).
Nada disso é reaplicado por acidente: `environments/{hom,prod}/` só
declaram os 5 recursos de workload por Lambda (function, role, policy,
log group, permission) e leem a plataforma via `terraform_remote_state`
(`remote_state.tf`).

Duas configurações independentes:

- `environments/prod/` — ambiente de **produção**
  (`api.jrnexpenses.com`), state em
  `key = gastosapp/prod/terraform.tfstate`.
- `environments/hom/` — ambiente de **homologação**
  (`api-hom.jrnexpenses.com`, FEAT-13), state em
  `key = gastosapp/hom/terraform.tfstate`.

Ambas usam o mesmo bucket S3 de state
(`gastosapp-terraform-state-648443184523`, com locking nativo via
`use_lockfile`), criado pelo `bootstrap/` que hoje vive em
[`infra-jrnexpenses/terraform/bootstrap/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/bootstrap).
Essa organização por ambiente replica o padrão já adotado pelo
Terraform do frontend (`frontend/infra/terraform/environments/{hom,prod}/`).

## Pré-requisitos

- Terraform >= 1.10 instalado localmente
- AWS CLI autenticado na conta do projeto (`648443184523`,
  região `us-east-1`)
- **Um profile com leitura de IAM de fato** (não o `agent-toolkit`
  usado por agentes de IA) — as roles de execução das Lambdas ficam
  neste diretório, e `agent-toolkit` recebe `AccessDenied` em
  `iam:GetRole`/`iam:GetRolePolicy` sobre elas (guardrail da conta, ver
  "Gotchas" em `backend/infra/CLAUDE.md`). Sem esse profile, `plan`/
  `apply` aqui falham na leitura de IAM antes mesmo de mostrar diff

## Como rodar

```bash
cd backend/infra/terraform/environments/hom   # ou environments/prod
terraform init \
  -backend-config="bucket=gastosapp-terraform-state-648443184523" \
  -backend-config="region=us-east-1"

AWS_PROFILE=<profile-com-leitura-de-iam> terraform plan
AWS_PROFILE=<profile-com-leitura-de-iam> terraform apply
```

A `key` do state já vem fixa em `versions.tf` de cada ambiente — não
precisa passar via `-backend-config`. Não há mais `bootstrap/` a rodar
neste monorepo: o bucket já existe e é criado/recriado a partir de
`infra-jrnexpenses/terraform/bootstrap/`, se necessário.

## Convenções

- Nenhum novo recurso Terraform deve ser criado sem pedido explícito do
  usuário (ver `backend/infra/CLAUDE.md`).
- `remote_state.tf` de cada ambiente expõe 5 `local.*`
  (`dynamodb_table_name`, `dynamodb_table_arn`, `cognito_user_pool_arn`,
  `ses_domain_identity_arn`, `api_gateway_execution_arn`), lidos do
  state `infra-jrnexpenses/{hom,prod}/terraform.tfstate` — usados nas
  policies IAM, na variável de ambiente `DynamoDb__TableName` e no
  `source_arn` de `aws_lambda_permission.apigateway`. Nunca referenciar
  um recurso de plataforma direto (ele não existe mais neste state).
- `aws_lambda_permission.apigateway` vive em `lambda.tf` de cada
  ambiente (não em um arquivo de API Gateway — esse arquivo migrou
  para a infra) — é workload, autoriza o API Gateway (do lado da
  infra) a invocar a Lambda.
- `data.aws_caller_identity.current` (em `lambda.tf`) é o único data
  source que sobra neste diretório — usado no ARN do Parameter Store
  das policies.

## Deploy da Lambda (FEAT-10)

A API .NET roda como Lambda Native AOT (runtime customizado
`provided.al2023`), atrás de um API Gateway HTTP API (agora do lado da
infra) — sem autorizador JWT no Gateway, autenticação continua só na
aplicação (FEAT-01).

Build e empacotamento (`infra/lambda/Dockerfile.build` +
`infra/lambda/build.sh`) rodam num container **Amazon Linux 2023** (a
mesma base do runtime da Lambda — necessário para compatibilidade de
glibc; a imagem oficial do SDK .NET, baseada em Ubuntu, gera um binário
que não roda na Lambda). O script gera `infra/lambda/function.zip`, que
o `lambda.tf` de **cada ambiente** referencia via `filename`/
`source_code_hash` (`${path.module}/../../../lambda/function.zip`) — é
o **mesmo artefato físico** para produção e homologação, já que os dois
ambientes rodam exatamente o mesmo código/contrato (ver FEAT-13).

**Desde a FEAT-14, o deploy real é automatizado via GitHub Actions**
(ver `backend/infra/CLAUDE.md`, seção "CI/CD") — os workflows publicam
o zip direto na Lambda via `aws lambda update-function-code` (sem
rodar `terraform apply`). O fluxo manual abaixo continua útil para
desenvolvimento local ou qualquer situação fora do fluxo automatizado:

```bash
cd backend
bash infra/lambda/build.sh   # gera infra/lambda/function.zip
cd infra/terraform/environments/prod   # ou environments/hom
AWS_PROFILE=<profile-com-leitura-de-iam> terraform plan
AWS_PROFILE=<profile-com-leitura-de-iam> terraform apply
```

Toda vez que o código da API mudar, repita esse fluxo para cada
ambiente que precisar do deploy — o `source_code_hash` no `lambda.tf`
muda junto com o zip, e o `terraform plan` mostra a atualização do
código como a única mudança. **Nunca fazer isso contra hom/prod sem
necessidade** — o deploy publicado pelo CI (`update-function-code` +
`update-function-configuration`, com `APP_VERSION`/`APP_COMMIT_SHA`/
`APP_ENVIRONMENT`) causa um drift esperado e aceito em `environment{}`
da Lambda da API em todo `plan` (ver `backend/infra/CLAUDE.md`) — um
`apply` reverteria o código publicado para o zip local desatualizado.

## Plataforma movida para `infra-jrnexpenses` (FEAT-40)

Tudo que **não** é workload das 3 Lambdas vive hoje em
[`infra-jrnexpenses`](https://github.com/jrneto/infra-jrnexpenses):

| O que | Onde |
|---|---|
| Bucket S3 de state (`bootstrap/`) | [`terraform/bootstrap/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/bootstrap) |
| Tabela DynamoDB, Cognito, Parameter Store, SES, API Gateway, domínio `api*` (ACM + DNS), por ambiente | [`terraform/environments/{hom,prod}/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/environments) (`backend-*.tf`) |
| Hosted zone `jrnexpenses.com.` | [`terraform/dns/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/dns) (migrada do frontend na FEAT-34) |
| Referência de OIDC Provider + IAM Role do CI/CD do backend | [`terraform/cicd/backend/`](https://github.com/jrneto/infra-jrnexpenses/tree/develop/terraform/cicd/backend) |

Detalhamento completo do mecanismo de migração (sem criar/destruir/
recriar nenhum recurso AWS) e das decisões técnicas:
`backend/specs/FEAT-40-extracao-infra-repo-apartado/` (`spec.md`,
`plan.md`, `tasks.md`) neste monorepo, e `CLAUDE.md`/`README.md` do
repositório novo.

**Achado durante o fechamento da FEAT-40 (etapa 8, 2026-09-13)**: ao
contrário do que a documentação anterior afirmava (`AccessDenied` em
toda tentativa de leitura/escrita de IAM, role sempre fora de state),
o objeto `gastosapp-backend/cicd/terraform.tfstate` **contém, sim**,
`aws_iam_role.backend_cicd` e `aws_iam_role_policy.backend_cicd`
gerenciados (serial 5, sem registro de quando/como esse `import`
aconteceu) — não foi apagado na limpeza dos states órfãos desta etapa
por não ser, de fato, órfão. Ver a seção correspondente em
`backend/infra/CLAUDE.md` e no `CLAUDE.md` do `infra-jrnexpenses` para
o estado real.

## Histórico (features anteriores à FEAT-40)

- **FEAT-09** — Cognito + Parameter Store trazidos para Terraform; em
  produção, o User Pool/App Client atuais foram **recriados** (não
  importados) — o pool anterior, criado manualmente, foi mantido
  intacto até exclusão manual pelo usuário. Os 3 parâmetros do
  Parameter Store de produção foram trazidos via `terraform import`.
  Em homologação (FEAT-13), todos os recursos foram criados do zero,
  sem import. Histórico preservado na migração da FEAT-40 (`state mv`,
  sem novo `import`).
- **FEAT-10** — Lambda Native AOT + API Gateway HTTP API.
- **FEAT-12** — domínio customizado `api.jrnexpenses.com` (ACM +
  mapeamento + DNS), 5 recursos trazidos via `terraform import`
  (nenhum novo criado). Hosted zone `jrnexpenses.com.` gerenciada pelo
  Terraform do frontend à época (hoje, `infra-jrnexpenses/terraform/dns/`).
- **FEAT-13** — ambiente de homologação, isolado de produção (tabela,
  Cognito, Parameter Store, Lambda, API Gateway e ACM próprios,
  emitido do zero via Terraform).
- **FEAT-14** — deploy automatizado via GitHub Actions; ver
  `backend/infra/CLAUDE.md`, seção "CI/CD".
- **FEAT-33** — SES com identidade de domínio própria por ambiente.
- **FEAT-38** — toggle de log de payload completo via Parameter Store;
  retenção de log differenciada hom (7 dias) × prod (14 dias).
