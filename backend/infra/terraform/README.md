# Terraform — backend/infra

Provisiona a infraestrutura AWS do backend: tabela DynamoDB (`GastosApp`
+ `GSI1` + `GSI2`, `dynamodb.tf`), Cognito User Pool + App Client
(`cognito.tf`), os parâmetros do Cognito no Parameter Store
(`parameter-store.tf`), a Lambda .NET Native AOT da API
(`lambda.tf`) e o API Gateway HTTP API que a expõe publicamente
(`api-gateway.tf`). Toda a infraestrutura do backend está sob Terraform
(ver `backend/specs/FEAT-09-terraform-cognito-parameter-store/` e
`backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/`).

Quatro configurações independentes:

- `bootstrap/` — cria o bucket S3 que guarda o state remoto das demais
  configurações. Mantém o **próprio state local** (não tem como o
  bucket gerenciar o state que o cria). Aplicado uma única vez por
  conta AWS (ou raramente, se precisar recriar o bucket).
- `environments/prod/` — ambiente de **produção**
  (`api.jrnexpenses.com`), state próprio no bucket criado pelo
  `bootstrap/` (`key = gastosapp/prod/terraform.tfstate`), usando o
  locking nativo do backend S3 (`use_lockfile`).
- `environments/hom/` — ambiente de **homologação**
  (`api-hom.jrnexpenses.com`, FEAT-13), isolado de produção (tabela,
  Cognito, Parameter Store, Lambda e API Gateway próprios), state
  próprio no mesmo bucket (`key = gastosapp/hom/terraform.tfstate`).
- `cicd/` — OIDC Provider (reaproveitado, não criado) + IAM Role usados
  pelos workflows de deploy do GitHub Actions
  (`backend/specs/FEAT-14-cicd-github-actions/`). **Provavelmente fora
  do state hoje** — mesmo gap de permissão já documentado para o
  frontend (ver seção dedicada abaixo).

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
- Cognito (`cognito.tf`) e Parameter Store (`parameter-store.tf`) são
  gerenciados por Terraform desde a FEAT-09. Em produção, o User
  Pool/App Client atuais foram **recriados** (não importados) — o pool
  anterior, criado manualmente, foi mantido intacto até exclusão manual
  pelo usuário. Os 3 parâmetros do Parameter Store de produção foram
  trazidos via `terraform import` (recurso simples, sem risco de
  dado). Em homologação (FEAT-13), todos os recursos são criados do
  zero via Terraform, sem import.

## Domínio customizado da API (FEAT-12)

Além da URL padrão do API Gateway, a API de produção responde em
`https://api.jrnexpenses.com`, gerido pelos arquivos `acm.tf`
(certificado ACM), `api-gateway-domain.tf` (`aws_apigatewayv2_domain_name`
+ `aws_apigatewayv2_api_mapping`) e `dns.tf` (records Route 53), dentro
de `environments/prod/`. Os 5 recursos já existiam manualmente na
conta e foram trazidos via `terraform import` — nenhum recurso novo foi
criado.

A hosted zone `jrnexpenses.com.` é gerenciada pelo Terraform do
**frontend** (`frontend/infra/terraform/dns/`, FEAT-07), não pelo
backend. `dns.tf` (em cada ambiente) só lê essa zona por nome
(`data "aws_route53_zone"`), sem duplicá-la ou geri-la, para poder
gerenciar os records de `api.jrnexpenses.com` (prod) ou
`api-hom.jrnexpenses.com` (hom) dentro dela. Ver
`backend/specs/FEAT-12-terraform-dominio-customizado-api/`.

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

## Ambiente de homologação (FEAT-13)

`environments/hom/` provisiona uma cópia isolada da infraestrutura de
produção, exposta em `https://api-hom.jrnexpenses.com`:

- Tabela DynamoDB própria: `GastosApp-Hom`
- Cognito User Pool + App Client próprios: `user-pool-gastos-app-hom`,
  `controle-gastos-spa-hom` — `callback_urls` usa um placeholder
  (`http://localhost:5173`), já que não existe frontend de
  homologação ainda
- Parameter Store em `/GastosApp/Hom/...` (em vez de `/GastosApp/...`)
  — a Lambda de hom recebe a variável de ambiente
  `ParameterStore__Path=/GastosApp/Hom/`, que sobrepõe o default
  `/GastosApp/` lido em produção (mudança em
  `AwsParameterStoreExtensions.cs`/`Program.cs`, sem alterar contrato
  de API)
- Tabela DynamoDB isolada via a variável de ambiente
  `DynamoDb__TableName=GastosApp-Hom` na Lambda de hom (produção não
  seta essa variável, cai no default `GastosApp`). Isso exigiu corrigir
  `InfrastructureServiceCollectionExtensions.cs`: o binding de
  `DynamoDbOptions` usava `services.Configure<T>(IConfiguration)`
  (reflection), que **falha silenciosamente sob Native AOT** — mesmo
  problema já corrigido para `CognitoOptions` na FEAT-10, mas nunca
  replicado para `DynamoDbOptions` até este achado durante a validação
  da FEAT-13. Nunca dava problema antes porque o default hardcoded
  coincidia com o nome real da tabela de produção
- Lambda (`gastos-app-api-hom`) e API Gateway HTTP API
  (`gastos-app-api-hom`) próprios, mesmo artefato de produção
- Certificado ACM próprio (`api-hom.jrnexpenses.com`), emitido do zero
  via Terraform (diferente de produção, importado já `ISSUED`) —
  `dns.tf` usa `aws_acm_certificate_validation` para esperar a
  validação DNS completar antes do domínio customizado usar o
  certificado
- CORS (`frontend_origins`) vazio por padrão — sem frontend de
  homologação, nenhuma origem de browser é liberada; chamadas via
  curl/Postman/testes automatizados não são afetadas

Ver `backend/specs/FEAT-13-ambiente-homologacao/` para a spec e o plano
técnico completos.

## `cicd/` — OIDC Provider (reaproveitado) + IAM Role do backend (FEAT-14)

`backend/infra/terraform/cicd/` contém a IAM Role
(`gastosapp-backend-cicd`) assumida via OIDC pelos workflows de deploy
(`.github/workflows/backend-deploy-{hom,prod}.yml`), com permissão
mínima (`lambda:UpdateFunctionCode`/`UpdateFunctionConfiguration`/
`GetFunction`/`GetFunctionConfiguration`) escopada só às duas funções
Lambda deste projeto. **Não cria um novo OIDC Provider** — `oidc.tf`
usa `data "aws_iam_openid_connect_provider"` para referenciar o
Provider já existente na conta (criado manualmente para o frontend na
FEAT-09, é um recurso único por conta/URL de emissor).

**Gap confirmado (2026-08-08, mesmo já documentado em
`frontend/infra/terraform/README.md`, seção "cicd/")**: `terraform
plan` falha já na leitura do OIDC Provider existente —
`AccessDenied: User: .../josereato-admin is not authorized to perform:
iam:ListOpenIDConnectProviders` — mesmo com o perfil
`AWSReservedSSO_Perfil-Admin-Desenvolvedor`. Nenhum recurso chegou a
ser criado (a falha é no `plan`, antes de qualquer `apply`). Mesmo
guardrail identificado no frontend, agora confirmado também para ações
de **leitura** sobre OIDC, não só criação.

A Role precisa ser criada **manualmente no console AWS**, com o JSON
abaixo (gerado a partir de `iam-role.tf`/`iam-policy.tf`, byte a byte
igual ao que o Terraform aplicaria):

**Trust policy** (`gastosapp-backend-cicd`):
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::648443184523:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": [
            "repo:jrneto/meus-gastos-pessoais:environment:backend-hom",
            "repo:jrneto/meus-gastos-pessoais:environment:backend-prod"
          ]
        }
      }
    }
  ]
}
```

**Policy inline** (`gastosapp-backend-cicd-deploy`):
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "UpdateBackendLambdaCode",
      "Effect": "Allow",
      "Action": [
        "lambda:UpdateFunctionCode",
        "lambda:UpdateFunctionConfiguration",
        "lambda:GetFunction",
        "lambda:GetFunctionConfiguration"
      ],
      "Resource": [
        "arn:aws:lambda:us-east-1:648443184523:function:gastos-app-api-hom",
        "arn:aws:lambda:us-east-1:648443184523:function:gastos-app-api"
      ]
    }
  ]
}
```

Passo a passo no console: IAM → Roles → Create role → Custom trust
policy (cola o JSON de trust acima) → Add permissions → Create inline
policy (cola o JSON de policy acima, nome
`gastosapp-backend-cicd-deploy`) → nome da Role:
`gastosapp-backend-cicd`.

Depois de criada, se a permissão de leitura for liberada no futuro,
importar pra trazer ao state:

```bash
cd backend/infra/terraform/cicd
terraform import aws_iam_role.backend_cicd gastosapp-backend-cicd
terraform import aws_iam_role_policy.backend_cicd \
  gastosapp-backend-cicd:gastosapp-backend-cicd-deploy
terraform plan   # deve dar "No changes" se o console bateu com o .tf
```

**Uso pelos workflows**: o ARN da Role é cadastrado como variável
`CICD_ROLE_ARN` nos GitHub Environments `backend-hom`/`backend-prod`
(distintos dos `hom`/`prod` já usados pelo frontend, pra não competir
pela mesma variável com uma Role diferente) — não depende do state do
Terraform para funcionar, só do recurso existir de fato na conta.

**Convenção de tag `backend-v*`**: como o repositório é compartilhado
com o frontend (que usa `vX.Y.Z`), as releases do backend usam o
prefixo `backend-v` — necessário pros workflows de deploy de produção e
de rascunho automático de release não se atropelarem entre os dois
contextos (ver `backend/specs/FEAT-14-cicd-github-actions/plan.md`,
decisão 5).
