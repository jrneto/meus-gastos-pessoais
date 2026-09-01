# Infra do Backend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs, e
[`/backend/CLAUDE.md`](../CLAUDE.md) para o contexto geral do backend.

## Princípios

- Produção e homologação são **100% AWS real**; ambiente local roda
  contra emuladores Docker (ver "Ambientes" abaixo). Ver também
  `backend/docs/constitution.md` e [`/docs/architecture.md`](../../docs/architecture.md).
- **Isolamento por configuração, não por convenção**: os endpoints locais
  (`Cognito:ServiceURL`, `DynamoDb:ServiceURL`, `ParameterStore:ServiceURL`)
  só existem em `appsettings.Development.json` e no SSM local — prod/hom
  nunca declaram essas chaves, então usam sempre AWS real/IAM Role.
- IaC **exclusivamente Terraform** (não CloudFormation, não CDK). **Só
  gerar/alterar `.tf` para um recurso quando pedido explicitamente pelo
  usuário.** Código vive em `backend/infra/terraform/`, com
  `environments/{prod,hom}/` (tabela DynamoDB, Cognito User Pool + App
  Client, Parameter Store, Lambda, API Gateway, domínio customizado —
  ver `backend/docs/data-model.md`) e `bootstrap/` à parte, mantendo
  state local próprio (chicken-and-egg do bucket S3 de state remoto,
  locking via `use_lockfile`). Passo a passo:
  `backend/infra/terraform/README.md`.
- O domínio `api.jrnexpenses.com`/`api-hom.jrnexpenses.com` está sob
  Terraform (ACM, mapeamento no API Gateway, records DNS). A hosted
  zone `jrnexpenses.com.` em si é gerenciada pelo Terraform do
  **frontend** (`frontend/infra/terraform/dns/`) — o backend só a lê
  via `data "aws_route53_zone"`, nunca a duplica/gerencia.

## Ambientes

| | Produção | Homologação | Local |
|---|---|---|---|
| API | `api.jrnexpenses.com` (Lambda + API GW) | `api-hom.jrnexpenses.com` (Lambda + API GW próprios, mesmo artefato) | `dotnet run` fora de Docker |
| Tabela | `GastosApp` | `GastosApp-Hom` | `GastosApp-Local` |
| Cognito | User Pool + App Client de prod | `user-pool-gastos-app-hom` / `controle-gastos-spa-hom` | `cognito-local` (Docker) |
| Parameter Store | `/GastosApp/...` (default) | `/GastosApp/Hom/...` via env `ParameterStore__Path` | `/GastosApp/...` no SSM local (LocalStack), + `ServiceURL`/`AccessKey`/`SecretKey` |
| ACM | importado, já `ISSUED` | emitido do zero | — |

- Tabela isolada por env `DynamoDb__TableName` na Lambda de cada ambiente.
- CORS de hom (`frontend_origins`, API Gateway) já aponta pro frontend
  de homologação real (`https://hom.jrnexpenses.com`, desde a
  FEAT-08/FEAT-11 do frontend). O `callback_urls` do **Cognito** de hom
  ainda não foi atualizado — continua `["http://localhost:5173"]`
  (placeholder de antes de existir um frontend de hom, `cognito.tf`) —
  trocar quando o login via Cognito precisar redirecionar pra
  `hom.jrnexpenses.com`.
- Local: `docker-compose.yml` sobe `localstack` (DynamoDB + SSM,
  Community/gratuita) e `cognito-local` (build próprio, sem imagem
  oficial — `backend/infra/cognito-local/Dockerfile`, pacote npm
  `cognito-local`, Cognito não existe na edição gratuita do
  LocalStack). `scripts/local-init.sh` faz o seed idempotente. Passo a
  passo: `backend/infra/README.md`.

## CI/CD (GitHub Actions)

`backend-feature-pr.yml` (PR automático branch→develop),
`backend-deploy-hom.yml` (deploy a cada push em `develop` + rascunho de
release) e `backend-deploy-prod.yml` (deploy disparado por GitHub
Release + PR automático develop→main). Testes integrados **não** fazem
parte desses dois pipelines — ver bullet abaixo.

- **Deploy fora do Terraform**: os workflows publicam via
  `aws lambda update-function-code`/`update-function-configuration`
  (incl. `APP_VERSION`/`APP_COMMIT_SHA`/`APP_ENVIRONMENT`) — nenhum
  `terraform apply` roda em CI. Por isso `lambda.tf` de cada ambiente
  **não declara** essas 3 chaves no bloco `environment{}`.
- **Auth via OIDC**: Role `gastosapp-backend-cicd`
  (`backend/infra/terraform/cicd/`), reaproveita o OIDC Provider único
  da conta (criado para o frontend). Ver gotcha de permissão abaixo.
- **Tag `backend-v*`** (não `vX.Y.Z`, que é do frontend) — evita colisão
  nos workflows de deploy/rascunho de release do outro contexto.
- **GitHub Environments** `backend-hom`/`backend-prod` (distintos de
  `hom`/`prod`, do frontend), variáveis `CICD_ROLE_ARN`/`FUNCTION_NAME`.
- **Testes integrados são sob demanda, não gate de pipeline (FEAT-29/32,
  revisto em 2026-09-01)**: `backend-deploy-hom.yml` e
  `backend-deploy-prod.yml` **não** rodam `GastosApp.IntegrationTests` —
  até 2026-09-01 rodavam (job `integration-tests` em hom bloqueando o
  rascunho de release, job `check-hom-integration-tests` em prod
  bloqueando o deploy), mas o volume de `SignUp` no Cognito que a suíte
  inteira faz por execução (~35, um por teste + convite) estourava o
  limite de e-mail padrão do Cognito (50/dia por conta AWS, sem SES
  configurado — compartilhado entre hom e prod), quebrando pipelines
  sem nenhum bug de verdade. Continuam **obrigatórios localmente**
  antes de dar uma feature por concluída (ver `backend/CLAUDE.md`), e
  disponíveis sob demanda, isolados por ambiente, via
  `workflow_dispatch`: `backend-integration-tests-hom.yml` e
  `backend-integration-tests-prod.yml` (aba Actions do GitHub, sem
  tocar build/deploy). A role `gastosapp-backend-cicd` mantém a
  permissão adicional que esses dois workflows usam —
  `cognito-idp:AdminConfirmSignUp`/`AdminDeleteUser` (User Pools hom/
  prod), `dynamodb:GetItem`/`Query`/`DeleteItem`/`BatchWriteItem`
  (tabelas hom/prod) e `ssm:GetParametersByPath` (prefixo
  `/GastosApp`) — nunca usada pela Lambda da aplicação. Ver
  `backend/specs/FEAT-29-testes-integrados/`.

## Gotchas conhecidos

- **`services.Configure<T>()` falha silenciosamente sob Native AOT**
  (binding via reflection não funciona no runtime `provided.al2023`) —
  todo `Options` lido de `IConfiguration` neste projeto usa leitura
  manual em `InfrastructureServiceCollectionExtensions.cs`
  (`CognitoOptions`, `DynamoDbOptions`), nunca `services.Configure<T>()`.
- **Git Bash/MSYS (Windows) reescreve argumentos começando com `/`**
  (ex.: `/GastosApp/...`) como caminho de arquivo Windows antes de
  chegar no AWS CLI, corrompendo nomes de parâmetro — scripts locais
  exportam `MSYS_NO_PATHCONV=1`.
- **Perfil `agent-toolkit` provavelmente sem `iam:CreateRole`/`PutRolePolicy`**
  — se `terraform apply` do OIDC Provider/Role falhar com `AccessDenied`,
  criar manualmente no console e conferir contra os `.tf`.

## Specs

Quando este contexto tiver specs próprias de infraestrutura, seguir o
mesmo padrão do backend: `backend/specs/{FEAT-XX-nome}/{spec.md,
plan.md, tasks.md}`, nunca arquivo solto — não crie árvore de specs
separada só para infra.
