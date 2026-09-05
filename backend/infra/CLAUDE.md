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

## E-mail transacional (SES, FEAT-33)

Cognito (cadastro, recuperação de senha) e as Lambdas do backend que
precisarem enviar e-mail diretamente (trigger de conta, API principal)
enviam via **Amazon SES**, com identidade de domínio própria por
ambiente — não mais o envio padrão do Cognito (sem marca própria e
limitado a 50 e-mails/dia por conta, compartilhado entre hom e prod,
ver "Testes integrados" abaixo).

- **Identidade verificada por ambiente**, com DKIM habilitado
  (`ses.tf` em cada `environments/{prod,hom}/`): prod verifica o
  domínio raiz `jrnexpenses.com`, hom verifica o subdomínio
  `hom.jrnexpenses.com` — mesmo padrão de separação por subdomínio já
  usado por `api.jrnexpenses.com`/`api-hom.jrnexpenses.com`. Os
  records DNS de verificação/DKIM vivem em `dns.tf` de cada ambiente,
  na hosted zone `jrnexpenses.com.` (gerenciada pelo frontend, lida
  só por `data "aws_route53_zone"`, mesmo mecanismo da FEAT-12).
- **`email_configuration` do `aws_cognito_user_pool`**
  (`email_sending_account = "DEVELOPER"`) aponta pra identidade do
  próprio ambiente. Remetente: `jrn.expenses <no-reply@jrnexpenses.com>`
  em prod, `jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>`
  em hom (nome de exibição com sufixo pra nunca confundir com prod
  durante teste manual).
- **IAM `ses:SendEmail`/`ses:SendRawEmail`** concedido só às duas
  Lambdas do backend (`lambda.tf` e `lambda-account-trigger.tf` de
  cada ambiente), escopado à identidade de domínio do próprio ambiente
  — nenhuma outra função ganha essa permissão.
- **Sandbox do SES**: a conta nasceu no sandbox (`ProductionAccessEnabled:
  false`, teto de 200 e-mails/dia, 1/s, só destinatários verificados
  manualmente). Pedido de saída enviado em 2026-09-01 via
  `aws sesv2 put-account-details` (`mail-type=TRANSACTIONAL`,
  `website-url=https://jrnexpenses.com`) — status na conclusão da
  FEAT-33: `ReviewDetails.Status = "PENDING"`. Conferir
  `aws sesv2 get-account --region us-east-1` antes de assumir que já
  saiu; enquanto pendente, e-mail pra um destinatário real (não
  verificado manualmente no SES) não é entregue, mesmo com o resto da
  infra correta.
- **Gotcha de deliverability**: validado manualmente em hom que o
  e-mail de confirmação chega, mas caiu na caixa de spam do Gmail —
  só DKIM foi configurado, sem MAIL FROM customizado (SPF) nem DMARC;
  ver débito técnico correspondente em `backend/docs/backlog.md`.
- Ver `backend/specs/FEAT-33-infra-email-transacional-ses/` para a
  spec/plano completos.
- **`Ses/SenderEmail` no Parameter Store** (FEAT-36): o email de "senha
  alterada" (`POST /auth/reset-password`) é enviado direto pela API via
  `ses:SendEmail` — fora do fluxo nativo do Cognito, então o backend
  precisa do remetente à mão em runtime. `/GastosApp/Ses/SenderEmail`
  (prod) e `/GastosApp/Hom/Ses/SenderEmail` (hom), tipo `String`,
  espelham o mesmo valor já calculado pelo `email_configuration` do
  User Pool (`parameter-store.tf` de cada ambiente). Sem equivalente
  local: LocalStack Community não emula SES (só o SSM genérico), e o
  envio deste email é best-effort (falha só loga, não derruba a
  resposta de sucesso do reset) — ver
  `backend/specs/FEAT-36-recuperacao-senha/`.
- **`Ses__SenderEmail` como variável de ambiente na Lambda de trigger de
  conta** (FEAT-37): o email de boas-vindas (`Post Confirmation`,
  `AccountTriggerHandler`) é enviado direto via `ses:SendEmail`, mesmo
  padrão da FEAT-36 — mas essa Lambda (`GastosApp.CognitoTriggers`) não
  lê Parameter Store (decisão da FEAT-19, ver `Function.cs`), então o
  remetente não pode vir de lá como na API. Em vez disso,
  `Ses__SenderEmail` é declarado direto no bloco `environment{}` de
  `aws_lambda_function.account_trigger` (`lambda-account-trigger.tf` de
  cada ambiente), com o mesmo literal fixo já usado em
  `aws_ssm_parameter.ses_sender_email`/`email_configuration` do Cognito
  — evita o mesmo diff perpétuo já descoberto na FEAT-36. Mesma
  limitação de ambiente local (sem SES no LocalStack Community): a
  chamada real cai no `catch` defensivo de `EnsureAccountCommandHandler`
  (só loga), validado manualmente via
  `AccountTriggerHandlerManualDebug.cs` — ver
  `backend/specs/FEAT-37-email-boas-vindas/`.

## Observabilidade (headers de API, FEAT-38)

Toda chamada de API aceita 4 headers opcionais de observabilidade
(`trace-id`, `session-id`, `client-platform`, `client-version`) —
`RequestObservabilityMiddleware` (`GastosApp.Api/Middlewares/`),
registrado antes até de `UseExceptionHandler()`. `trace-id` é sempre
ecoado na resposta (gerado pela API quando ausente); o log estruturado
(JSON via Serilog `JsonFormatter`, todo ambiente inclusive dev local)
inclui payload completo em erro (4xx/5xx, qualquer content-type JSON —
inclusive `application/problem+json`) ou quando o toggle abaixo está
ligado.

- **`Logging/FullPayloadLoggingEnabled` no Parameter Store** (`String`,
  `"true"`/`"false"`, default `"false"`):
  `/GastosApp/Logging/FullPayloadLoggingEnabled` (prod),
  `/GastosApp/Hom/Logging/FullPayloadLoggingEnabled` (hom) — mesmo
  padrão dos demais parâmetros Cognito/CORS/SES já existentes. Lido uma
  única vez por cold start (`LoggingOptions`, mesma limitação dos
  demais `Options` deste projeto) — ligar/desligar não tem efeito
  imediato em Lambdas já "quentes", só nas próximas que passarem por
  cold start.
- **CORS do API Gateway** (`cors_configuration` de
  `aws_apigatewayv2_api.main`, `api-gateway.tf` de cada ambiente)
  precisou ganhar os 4 headers novos em `allow_headers` + `trace-id` em
  `expose_headers` — sem isso, o preflight do navegador recusaria esses
  headers antes de chegar na Lambda.
- **Retenção de log group deixou de ser uniforme entre hom/prod**: hom
  passou a 7 dias (`retention_in_days`), prod permanece em 14 (15,
  cogitado originalmente, não é um valor aceito pela API da AWS — só um
  conjunto fixo: 1, 3, 5, 7, 14, 30, 60...). Aplica-se às 3 Lambdas do
  backend por ambiente (API principal + os 2 triggers do Cognito).

Ver `backend/specs/FEAT-38-observabilidade-headers-api/` para a
spec/plano completos.

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
- **Mesmo guardrail de IAM também bloqueia leitura de role já existente**
  (achado na FEAT-33): `terraform plan`/`apply` com o perfil
  `agent-toolkit` falha com `AccessDenied` em `iam:GetRole`/
  `iam:GetRolePolicy` sobre `jrnexpenses-account-trigger-lambda-exec`
  (role criada pelo próprio Terraform na FEAT-19, não é o caso do OIDC
  acima) — não é só sobre criação/OIDC, é uma restrição mais ampla de
  leitura de IAM para essa role específica com esse perfil. Contorno
  pra `plan`/`apply` que não mexem em IAM: `-refresh=false` +
  `-target=<recursos não-IAM>`. Para `apply` que precisa criar/alterar
  IAM (ex.: `aws_iam_role_policy`), rodar localmente com um profile com
  permissão de IAM de fato (não `agent-toolkit`) — conferir o resultado
  aplicado via `terraform state show <recurso>` (não depende de
  `iam:Get*`) quando a leitura via AWS CLI/console também estiver
  bloqueada.

## Specs

Quando este contexto tiver specs próprias de infraestrutura, seguir o
mesmo padrão do backend: `backend/specs/{FEAT-XX-nome}/{spec.md,
plan.md, tasks.md}`, nunca arquivo solto — não crie árvore de specs
separada só para infra.
