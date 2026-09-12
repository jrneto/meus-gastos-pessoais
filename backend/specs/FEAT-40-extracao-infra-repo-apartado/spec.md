# FEAT-40: Extração da infra Terraform do backend para o repositório `infra-jrnexpenses`

## Objetivo

Separar a infraestrutura de **plataforma** do backend (DynamoDB, Cognito,
Parameter Store, SES, API Gateway + domínio customizado/ACM/DNS, roles de
CI/CD, bootstrap do state) da infraestrutura de **workload** (o que o
deploy de fato toca: as funções Lambda, suas roles de execução, log
groups e permissões de invocação), levando a plataforma para um
repositório novo e apartado, `infra-jrnexpenses`, de forma **paulatina e
controlada** — um state por vez, sem criar, destruir ou recriar nenhum
recurso AWS, com `terraform plan` = "No changes" dos dois lados ao final
de cada etapa e caminho de rollback conhecido.

O monorepo continua dono só do Terraform essencial ao deploy da API e das
Lambdas de trigger do Cognito; tudo o mais passa a viver em
`infra-jrnexpenses`.

## Contexto

### Situação atual (levantada em 2026-09-12)

`backend/infra/terraform/` tem 4 configurações raiz independentes, sem
módulos nem workspaces, todas com apply manual/local (nenhum workflow de
CI executa Terraform — ver `backend/infra/CLAUDE.md`, "Deploy fora do
Terraform"):

| Configuração | State | Conteúdo |
|---|---|---|
| `bootstrap/` | local (`terraform.tfstate` em disco, gitignored) | bucket S3 de state `gastosapp-terraform-state-648443184523` |
| `cicd/` | `gastosapp-backend/cicd/terraform.tfstate` (**vazio**) | OIDC provider (só `locals`) + role `gastosapp-backend-cicd` — referência, fora de state por causa do guardrail de IAM |
| `environments/hom/` | `gastosapp/hom/terraform.tfstate` | DynamoDB, Cognito, 6 SSM, 3 Lambdas (+ roles, policies, log groups, permissions), API Gateway, ACM, domínio `api-hom`, records DNS, SES |
| `environments/prod/` | `gastosapp/prod/terraform.tfstate` | idem, para prod (`api.jrnexpenses.com`, tabela `GastosApp`) |

Os workflows (`backend-deploy-{hom,prod}.yml`, os 4 dos triggers do
Cognito e os 2 de testes integrados) publicam só via `aws lambda
update-function-code`/`update-function-configuration`, com
`FUNCTION_NAME`/`*_FUNCTION_NAME`/`CICD_ROLE_ARN` cadastrados à mão nos
GitHub Environments `backend-hom`/`backend-prod`. **Nenhum valor de
`terraform output` é consumido por CI** — a extração não muda nada nos
workflows nem nos GitHub Environments.

### Feature irmã no frontend

A parte do frontend (CloudFront, OAC, ACM, WAF, hosted zone e records da
camada `dns/`, `cicd/` do frontend) é a
`frontend/specs/FEAT-33-extracao-infra-repo-apartado/`. As duas specs
compartilham o repositório de destino, a governança e o mecanismo de
migração, mas cada uma lista só as suas etapas e é entregue em branch/PR
próprios. Ordem combinada entre as duas: frontend antes do backend (hom
antes de prod em ambos), porque o frontend tem menos recursos por state e
serve para validar o padrão de referência cruzada antes de mexer no
state que contém Cognito/DynamoDB de produção.

### Decisões fechadas com o usuário

- **Fronteira** — vai para `infra-jrnexpenses`: DynamoDB, Cognito (User
  Pool + App Client), Parameter Store (todos os `aws_ssm_parameter`), SES
  (identidade, DKIM, verificação), API Gateway (API, integração, rota,
  stage), domínio customizado `api*` (ACM, validação, domain name, api
  mapping), records DNS de `api*`/SES (e o `data "aws_route53_zone"` que
  eles usam), `cicd/` e `bootstrap/`. **Fica no monorepo**: as 3
  `aws_lambda_function` (`api`, `account_trigger`,
  `custom_message_trigger`), suas `aws_iam_role`/`aws_iam_role_policy`,
  seus `aws_cloudwatch_log_group` e os 3 `aws_lambda_permission`
  (`apigateway`, `cognito_invoke_account_trigger`,
  `cognito_invoke_custom_message_trigger`).
- **Repositório novo nasce limpo** (commit inicial, sem reescrever
  histórico) — o histórico dos `.tf` continua acessível no monorepo.
- **Governança inalterada**: apply continua manual/local, cada
  `apply`/`state push` é aprovado individualmente pelo usuário, nenhum
  Terraform passa a rodar em CI.
- **Direção única de dependência: monorepo → infra, nunca o inverso.**
  O monorepo lê o state da infra via `terraform_remote_state` (mesmo
  bucket, padrão já usado em `frontend/infra/terraform/dns/`); a infra
  referencia as Lambdas do monorepo só por ARN determinístico construído
  a partir do nome da função (o `lambda_config` do Cognito e a
  `integration_uri` do API Gateway) — valores idênticos aos atuais, sem
  diff no plan.
- **States da infra ganham `key`s novas** no mesmo bucket
  (`infra-jrnexpenses/backend/{hom,prod}/terraform.tfstate`); os states
  do monorepo mantêm as `key`s atuais e só perdem os recursos movidos.
- **Mecanismo de movimentação**: `terraform state pull` nos dois lados →
  `terraform state mv -state=… -state-out=…` offline (não chama AWS, não
  esbarra no guardrail de IAM, cobre recursos não importáveis como
  `aws_ses_domain_identity_verification` e
  `aws_acm_certificate_validation`) → `terraform state push` nos dois
  lados → `plan` = "No changes" em ambos. Detalhamento e scripts por
  etapa são escopo do `plan.md`.

Precedentes de estilo/abordagem para infra sem criação de recurso:
`frontend/specs/FEAT-07-terraform-import-infra/` (reconciliação sem
mudar comportamento, arquitetura de camadas persistente/efêmera) e
`backend/specs/FEAT-12-terraform-dominio-customizado-api/` (leitura
cross-contexto de recurso de outro state).

## Requisitos de negócio / restrições

- **Nenhum recurso AWS criado, destruído, recriado ou alterado** por esta
  feature — só movimentação de state e reorganização de código Terraform.
  Qualquer `plan` que mostre `create`/`destroy`/`replace`/`update` em um
  recurso movido é bloqueio: investigar e corrigir o código antes de
  qualquer `apply`.
- **Custo zero**: nenhum recurso novo na conta. O único artefato novo na
  AWS são objetos de state (`key`s novas) no bucket já existente.
- **Cada etapa é atômica e verificável**: um state por etapa; ao final,
  `terraform plan` = "No changes" tanto na configuração da infra quanto
  na do monorepo, e o deploy de hom (`backend-deploy-hom.yml`) continua
  verde sem nenhuma alteração em workflow ou GitHub Environment.
- **Rollback conhecido antes de cada `state push`**: o bucket de state é
  versionado — restaurar a versão anterior do objeto reverte o state; o
  código reverte via git. Enquanto um recurso existir nos dois states
  (entre o push da infra e o push do monorepo), nenhum `apply` pode rodar
  em nenhum dos dois lados.
- **Nenhuma execução sem aprovação prévia explícita do usuário** — vale
  para `state push`, `apply` e qualquer comando que altere state remoto
  ou recurso real. `plan`, `state pull` e `state list` são leitura e podem
  rodar livremente. A lista de endereços a mover em cada etapa é revisada
  pelo usuário antes de executar.
- **Nenhuma mudança de comportamento observável de API** — nenhum
  endpoint, contrato, variável de ambiente ou parâmetro do Parameter
  Store muda de nome ou valor.
- **Hom antes de prod**, sempre: a etapa de prod só começa depois da de
  hom estar concluída, validada por deploy verde e smoke manual da API de
  hom.
- **Guardrail de IAM do perfil `agent-toolkit`** (ver
  `backend/infra/CLAUDE.md`, "Gotchas"): as roles de execução Lambda
  ficam no monorepo, então o `plan` do monorepo continua precisando de um
  profile com leitura de IAM (ou `-refresh=false`), como hoje. As
  configurações da infra não contêm IAM gerenciado (`cicd/` continua
  referência) e devem planejar limpas com `agent-toolkit`.
- **Docs coerentes ao fim**: `backend/infra/CLAUDE.md`,
  `backend/infra/terraform/README.md`, `/CLAUDE.md` raiz (seção
  "Infraestrutura" e a afirmação "não existe infraestrutura compartilhada
  entre contextos") e `/docs/architecture.md` passam a refletir que a
  plataforma vive em `infra-jrnexpenses` e o monorepo só gerencia o
  workload.

## Impacto observável externamente

Nenhum. Não há endpoint, contrato de API, variável de ambiente, parâmetro
do Parameter Store, nome de função, URL ou GitHub Environment alterado.
O único efeito visível é onde o código Terraform vive e em qual state
cada recurso está registrado.

## User Stories

**US1 — Repositório `infra-jrnexpenses` criado com esqueleto (compartilhada com a FEAT-33 do frontend)**
- Given o repositório `infra-jrnexpenses` ainda não existe
- When o usuário cria o repositório no GitHub e a etapa 0 é concluída
- Then ele contém commit inicial com `CLAUDE.md` (governança: apply
  manual, aprovação explícita, guardrail de IAM, mapa de states e ordem
  de dependência), `README.md` (init/plan/apply por configuração),
  `.gitignore` de Terraform e a árvore `terraform/` vazia, sem nenhum
  state remoto tocado

**US2 — `bootstrap/` e `cicd/` movidos como arquivos (compartilhada com a FEAT-33)**
- Given `backend/infra/terraform/bootstrap/` (state local) e
  `backend/infra/terraform/cicd/` (referência, state vazio) existem no
  monorepo
- When a etapa 1 é concluída
- Then ambos vivem em `infra-jrnexpenses/terraform/` (o
  `terraform.tfstate` local do bootstrap copiado fisicamente, continua
  gitignored), o JSON de referência da role de CI/CD no README reflete a
  policy atual (6 funções, 4 statements), as pastas foram removidas do
  monorepo e nenhum state remoto foi alterado

**US3 — Plataforma de hom movida para `infra-jrnexpenses/terraform/backend/hom/`**
- Given o state `gastosapp/hom/terraform.tfstate` contém plataforma e
  workload juntos
- When a etapa de hom é concluída
- Then DynamoDB, Cognito (pool + client), os 6 SSM, SES (identidade,
  DKIM, verificação), API Gateway (api, integração, rota, stage), ACM
  `api-hom`, validação, domain name, api mapping, `data.aws_route53_zone`
  e os records DNS (`api_hom_acm_validation`, `api_hom_a`,
  `ses_verification`, `ses_dkim[0..2]`) estão no state
  `infra-jrnexpenses/backend/hom/terraform.tfstate`, e o monorepo mantém
  só as 3 Lambdas, 3 roles, 3 policies, 3 log groups e 3 permissions —
  com `terraform plan` = "No changes" nos dois lados

**US4 — Monorepo de hom lê a plataforma via `terraform_remote_state`**
- Given as policies das Lambdas e as `aws_lambda_permission` de hom
  referenciavam ARNs de DynamoDB, Cognito, SES e API Gateway do mesmo
  state
- When a etapa de hom é concluída
- Then esses ARNs (e o nome da tabela em `DynamoDb__TableName`) vêm de
  outputs do state da infra via `terraform_remote_state`, sem nenhum
  valor hardcoded novo, e a infra não lê nada do state do monorepo — as
  referências da plataforma às Lambdas (`lambda_config` do Cognito,
  `integration_uri` do API Gateway) são ARNs construídos a partir do
  nome da função

**US5 — Plataforma de prod movida, mesmo padrão de hom**
- Given a etapa de hom está concluída e validada (deploy verde + smoke
  manual em `api-hom.jrnexpenses.com`)
- When a etapa de prod é concluída, em janela combinada com o usuário
- Then os recursos equivalentes de prod (incluindo os 2 SSM de CORS,
  `aws_acm_certificate.api` sem `aws_acm_certificate_validation`, e a
  `aws_lambda_function.api` de prod mantida **sem** bloco
  `environment{}`) estão em
  `infra-jrnexpenses/backend/prod/terraform.tfstate`, com `terraform
  plan` = "No changes" nos dois lados

**US6 — Nenhuma regressão no deploy e na API**
- Given os workflows de deploy e testes integrados não foram alterados
- When cada etapa de hom/prod é concluída
- Then `backend-deploy-hom.yml` (e, para prod, um deploy de release
  quando houver) roda verde, a API responde normalmente no domínio do
  ambiente e `backend-integration-tests-hom.yml` (sob demanda) passa

**US7 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que altere state remoto ou recurso real
  (`terraform state push`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado, revisa a lista de endereços/`plan` e
  aprova explicitamente antes da execução — nada roda de forma autônoma

**US8 — Documentação coerente ao fim (compartilhada com a FEAT-33)**
- Given `backend/infra/CLAUDE.md`, `backend/infra/terraform/README.md`,
  `/CLAUDE.md` raiz e `/docs/architecture.md` descrevem toda a infra como
  vivendo no monorepo
- When a última etapa é concluída
- Then esses documentos descrevem a divisão plataforma
  (`infra-jrnexpenses`) × workload (monorepo), a direção de dependência
  entre states e apontam para o repositório novo para tudo o que saiu

## Critérios de aceite

- [ ] Repositório `infra-jrnexpenses` criado pelo usuário, com commit
      inicial contendo `CLAUDE.md`, `README.md`, `.gitignore` e
      `terraform/` (etapa 0, compartilhada com a FEAT-33)
- [ ] `bootstrap/` e `cicd/` do backend vivem em `infra-jrnexpenses`;
      pastas removidas de `backend/infra/terraform/`; nenhum state remoto
      alterado nessa etapa; JSON da role de CI/CD no README atualizado
- [ ] `terraform state list` de `infra-jrnexpenses/terraform/backend/hom/`
      contém exatamente os recursos de plataforma de hom listados em US3;
      `terraform state list` de `backend/infra/terraform/environments/hom/`
      contém exatamente Lambdas, roles, policies, log groups e permissions
- [ ] `terraform plan` = "No changes" em
      `infra-jrnexpenses/terraform/backend/hom/` e em
      `backend/infra/terraform/environments/hom/` após a etapa de hom
- [ ] O mesmo para prod, após a etapa de prod
- [ ] Monorepo lê ARNs/nomes da plataforma via `terraform_remote_state`
      (outputs `dynamodb_table_name`, `dynamodb_table_arn`,
      `cognito_user_pool_arn`, `ses_domain_identity_arn`,
      `api_gateway_execution_arn`, ou equivalentes definidos no `plan.md`);
      a infra não contém `terraform_remote_state` apontando para state do
      monorepo
- [ ] Nenhum recurso AWS criado, destruído, recriado ou alterado —
      confirmado pelos `plan` acima e pela ausência de `apply` com
      mudanças em qualquer etapa
- [ ] Nenhuma alteração em `.github/workflows/*` nem nos GitHub
      Environments `backend-hom`/`backend-prod`; `backend-deploy-hom.yml`
      verde após a etapa de hom; API de hom respondendo (smoke manual)
- [ ] `backend-integration-tests-hom.yml` executado sob demanda e verde
      após a etapa de hom
- [ ] Nenhum `terraform state push`/`apply` executado sem aprovação
      explícita do usuário no momento da execução
- [ ] `backend/infra/CLAUDE.md`, `backend/infra/terraform/README.md`,
      `/CLAUDE.md` raiz e `/docs/architecture.md` atualizados (etapa
      final, compartilhada com a FEAT-33)
- [ ] `backend/docs/backlog.md` atualizado: FEAT-40 marcada como concluída

## Fora do escopo

- A parte do frontend (CloudFront, OAC, ACM, WAF, `dns/`, `cicd/` do
  frontend) — é a `frontend/specs/FEAT-33-extracao-infra-repo-apartado/`
- Qualquer mudança de valor/configuração dos recursos movidos (retenção
  de log, throttling, CORS, política de senha etc.) — só movimentação
- Pipeline de CI para `terraform fmt`/`validate`/`plan` no repositório
  novo — apply continua manual; pode virar melhoria futura se o usuário
  quiser
- Consolidar states (ex.: juntar backend e frontend de um mesmo ambiente
  em um único state, ou substituir o `data "aws_route53_zone"` do backend
  pela `aws_route53_zone.main` de `dns/` agora que vivem no mesmo repo) —
  otimização futura, não pré-requisito
- Criar módulos Terraform reutilizáveis ou lógica condicional de
  ambiente — hom e prod continuam como configurações paralelas, como hoje
- Trazer `cicd/` para dentro de state (`import` da role/OIDC) — continua
  bloqueado pelo guardrail de IAM, permanece referência
- Preservar histórico git dos `.tf` no repositório novo — decisão: nasce
  limpo
- Qualquer alteração em workflows de deploy, GitHub Environments ou no
  fluxo de release
