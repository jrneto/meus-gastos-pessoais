# Infra do Backend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs, e
[`/backend/CLAUDE.md`](../CLAUDE.md) para o contexto geral do backend.

## Princípios

- Toda infraestrutura de **produção e homologação** é **100% AWS real**
  — ver `backend/docs/constitution.md` e `backend/docs/architecture.md`.
- Desde a FEAT-18, **desenvolvimento local roda contra serviços
  emulados em Docker** (`backend/infra/docker-compose.yml`): LocalStack
  (DynamoDB + SSM Parameter Store, edição Community/gratuita) e
  **cognito-local** (Cognito, indisponível na edição gratuita do
  LocalStack — ver `backend/infra/cognito-local/`). Isso substitui a
  tentativa anterior baseada em LocalStack/Kong (ver "Estado legado"
  abaixo) e elimina a dependência de credenciais AWS reais para
  desenvolvimento do dia a dia. Passo a passo em
  `backend/infra/README.md`.
- **Isolamento garantido por configuração, não por convenção**: os
  endpoints locais (`Cognito:ServiceURL`, `DynamoDb:ServiceURL`,
  `ParameterStore:ServiceURL`) só existem em
  `appsettings.Development.json` e nos parâmetros seedados no SSM
  local — produção e homologação nunca declaram essas chaves, então o
  comportamento real (AWS real, credenciais do ambiente/IAM Role)
  nunca muda.
- IaC feito **exclusivamente em Terraform** — não CloudFormation, não
  CDK. **Só gerar/alterar código Terraform para um recurso quando
  solicitado explicitamente pelo usuário.**
- Provisionamento via Terraform vive em `backend/infra/terraform/`,
  organizado por ambiente desde a FEAT-13
  (`environments/prod/`, `environments/hom/`): tabela DynamoDB
  (`GastosApp` + `GSI1` + `GSI2`), Cognito User Pool + App Client e
  parâmetros do Parameter Store — ver `backend/docs/architecture.md`,
  `backend/docs/data-model.md` e
  `backend/specs/FEAT-09-terraform-cognito-parameter-store/`. State
  remoto em bucket S3 (locking nativo do backend S3, `use_lockfile` —
  sem tabela DynamoDB extra só para lock), com `key` distinta por
  ambiente, criado por um módulo `bootstrap/` separado (fora da divisão
  por ambiente) que mantém o próprio state local (chicken-and-egg do
  bucket que guarda seu próprio state). Passo a passo completo em
  `backend/infra/terraform/README.md`.
- Cognito e Parameter Store estão sob Terraform desde a FEAT-09 (antes
  eram provisionados manualmente). Qualquer novo recurso ou mudança
  ainda exige pedido explícito do usuário.
- O domínio customizado da API (`api.jrnexpenses.com`) está sob
  Terraform desde a FEAT-12: certificado ACM (`acm.tf`), domínio
  customizado + mapeamento do API Gateway (`api-gateway-domain.tf`) e
  os records DNS correspondentes (`dns.tf`). A hosted zone
  `jrnexpenses.com.` continua gerenciada pelo Terraform do **frontend**
  (`frontend/infra/terraform/dns/`, FEAT-07) — o backend só a lê via
  `data "aws_route53_zone"` (por nome), sem duplicá-la ou geri-la — ver
  `backend/specs/FEAT-12-terraform-dominio-customizado-api/`.
- Desde a FEAT-13, `backend/infra/terraform/` está organizado em
  **duas configurações por ambiente**, cada uma com state próprio no
  mesmo bucket (`environments/prod/` e `environments/hom/`), replicando
  o padrão já adotado pelo Terraform do frontend
  (`frontend/infra/terraform/environments/prod/`). `bootstrap/`
  continua fora dessa divisão (não é por ambiente).

## Ambiente de homologação (FEAT-13)

Além de produção (`environments/prod/`, `api.jrnexpenses.com`), existe
um ambiente de homologação completo e isolado em `environments/hom/`,
exposto em `https://api-hom.jrnexpenses.com` — ver
`backend/specs/FEAT-13-ambiente-homologacao/`.

Isolamento total entre os dois ambientes:
- Tabela DynamoDB própria (`GastosApp-Hom`, mesmo modelo de dados de
  produção)
- Cognito User Pool + App Client próprios (`user-pool-gastos-app-hom`,
  `controle-gastos-spa-hom`)
- Parameter Store em prefixo distinto (`/GastosApp/Hom/...` vs.
  `/GastosApp/...`) — a Lambda de hom lê esse prefixo via a variável de
  ambiente `ParameterStore__Path`, que sobrepõe o default
  `/GastosApp/` usado em produção (mudança em
  `AwsParameterStoreExtensions.cs`/`Program.cs`, sem alterar contrato
  de API)
- Tabela DynamoDB isolada via a variável de ambiente
  `DynamoDb__TableName` na Lambda de hom. Achado durante a validação da
  FEAT-13: o binding de `DynamoDbOptions` via `services.Configure<T>()`
  falha silenciosamente sob Native AOT (mesmo problema já corrigido
  para `CognitoOptions` na FEAT-10) — corrigido em
  `InfrastructureServiceCollectionExtensions.cs` para leitura manual,
  mesmo padrão do Cognito. Qualquer novo `Options` lido de
  `IConfiguration` neste projeto deve seguir esse padrão manual, nunca
  `services.Configure<T>()`
- Lambda + API Gateway HTTP API próprios (`gastos-app-api-hom`),
  publicando o **mesmo artefato** (`infra/lambda/function.zip`) e o
  mesmo código/contrato de produção — não há build separado para hom
- Certificado ACM próprio (`api-hom.jrnexpenses.com`, emitido do zero,
  diferente de produção que foi importado já `ISSUED`)

Sem frontend de homologação ainda: CORS (`frontend_origins`) e
`callback_urls` do Cognito de hom usam valores de baixo risco
(`[]` e `http://localhost:5173`, respectivamente) — trocar quando
existir um frontend de homologação real.

## Esteira de CI/CD (FEAT-14)

Desde a FEAT-14, o deploy do backend (build Native AOT + publicação na
Lambda) é automatizado via GitHub Actions, replicando as regras já
validadas no frontend (FEAT-09/10/11):
`.github/workflows/backend-feature-pr.yml` (PR automático
branch→develop), `backend-deploy-hom.yml` (deploy automático em hom a
cada push em `develop`, + rascunho de release) e
`backend-deploy-prod.yml` (deploy disparado por GitHub Release, + PR
automático develop→main) — ver
`backend/specs/FEAT-14-cicd-github-actions/`.

- **Deploy real feito fora do Terraform**: os workflows publicam o
  artefato via `aws lambda update-function-code` e definem
  `APP_VERSION`/`APP_COMMIT_SHA`/`APP_ENVIRONMENT` via
  `aws lambda update-function-configuration` (com merge das variáveis
  já geridas pelo Terraform) — nenhum `terraform apply` roda em CI, só
  em execução manual e aprovada. Consequência: `lambda.tf` de cada
  ambiente **não deve declarar** essas 3 chaves no bloco
  `environment{}`, para não competir com o que o pipeline acabou de
  publicar.
- **Autenticação AWS via OIDC**: IAM Role dedicada
  `gastosapp-backend-cicd` (`backend/infra/terraform/cicd/`),
  reaproveitando o OIDC Provider do GitHub Actions já existente na
  conta (criado para o frontend na FEAT-09 — é um recurso único por
  conta, não por contexto). Mesmo gap conhecido do frontend é esperado
  aqui: o perfil `agent-toolkit` provavelmente não tem permissão de
  `iam:CreateRole`/`PutRolePolicy` — se `apply` falhar por
  `AccessDenied`, a Role é criada manualmente no console, conferida
  contra os `.tf`.
- **Convenção de tag `backend-v*`**: como o repositório é compartilhado
  com o frontend (que usa `vX.Y.Z`), releases do backend usam o
  prefixo `backend-v` para não colidir em nenhum dos dois workflows
  (deploy de produção, rascunho automático de release).
- **GitHub Environments dedicados**: `backend-hom`/`backend-prod`
  (distintos de `hom`/`prod`, já usados pelo frontend), cada um com as
  variáveis `CICD_ROLE_ARN` e `FUNCTION_NAME`.

## Ambiente local (FEAT-18)

`docker-compose.yml` sobe dois serviços: `localstack` (DynamoDB + SSM
Parameter Store) e `cognito-local` (build próprio a partir do pacote
npm `cognito-local`, `backend/infra/cognito-local/Dockerfile` — não há
imagem oficial mantida no Docker Hub). `scripts/local-init.sh`
orquestra a criação idempotente do User Pool local, da tabela
`GastosApp-Local` e dos parâmetros em `/GastosApp/` no SSM local
(mesma estrutura de produção/homologação, mais
`ServiceURL`/`AccessKey`/`SecretKey`, que sinalizam "modo local" pro
backend). A API roda fora do Docker (`dotnet run`), apontando pra esses
containers via `appsettings.Development.json`. Passo a passo completo
em `backend/infra/README.md`.

Achado durante a implementação: no Git Bash/MSYS (Windows), argumentos
de linha de comando começando com `/` (ex.: `/GastosApp/...`) são
reescritos como caminho de arquivo Windows antes de chegar no AWS CLI,
corrompendo nomes de parâmetro silenciosamente — os scripts exportam
`MSYS_NO_PATHCONV=1` para evitar isso (sem efeito em bash real).

Esta é a substituição definitiva da tentativa anterior baseada em
LocalStack/Kong (removida nesta feature — Kong não faz parte da
solução atual, e as suposições de LocalStack para Cognito estavam
desatualizadas — ver `backend/specs/FEAT-18-ambiente-local-sem-aws/`).

## Specs

Quando este contexto começar a ter specs próprias de infraestrutura,
seguir o mesmo padrão do restante do backend:
`backend/specs/{FEAT-XX-nome}/{spec.md, plan.md, tasks.md}`, nunca
arquivo solto — não crie uma árvore de specs separada só para infra.
