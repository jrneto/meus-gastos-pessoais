# FEAT-40: Extração da infra Terraform do backend para o repositório `infra-jrnexpenses`

## Objetivo

Separar a infraestrutura de **plataforma** do backend (DynamoDB, Cognito,
Parameter Store, SES, API Gateway + domínio customizado/ACM/DNS, roles de
CI/CD, bootstrap do state) da infraestrutura de **workload** (o que o
deploy de fato toca: as funções Lambda, suas roles de execução, log
groups e permissões de invocação), levando a plataforma para o
repositório apartado `infra-jrnexpenses` — já criado e populado com a
parte do frontend pela FEAT-34 — de forma **paulatina e controlada**: um
state por vez, sem criar, destruir ou recriar nenhum recurso AWS, com
`terraform plan` = "No changes" dos dois lados ao final de cada etapa e
caminho de rollback conhecido.

Ao final, o monorepo é dono só do Terraform essencial ao deploy da API e
das Lambdas de trigger do Cognito; tudo o mais vive em
`infra-jrnexpenses`, e a extração iniciada pela FEAT-34 fica **completa**
(docs raiz e do repositório novo deixam de dizer "hoje só a parte do
frontend migrou").

## Contexto

### Situação atual (revisada em 2026-09-12, após a conclusão da FEAT-34)

`backend/infra/terraform/` tem 4 configurações raiz independentes, sem
módulos nem workspaces, todas com apply manual/local (nenhum workflow de
CI executa Terraform — ver `backend/infra/CLAUDE.md`, "Deploy fora do
Terraform"):

| Configuração | State | Conteúdo |
|---|---|---|
| `bootstrap/` | local (`terraform.tfstate` em disco, gitignored) | bucket S3 de state `gastosapp-terraform-state-648443184523` |
| `cicd/` | `gastosapp-backend/cicd/terraform.tfstate` (**vazio**) | OIDC provider (só `locals`) + role `gastosapp-backend-cicd` — referência, fora de state por causa do guardrail de IAM |
| `environments/hom/` | `gastosapp/hom/terraform.tfstate` | DynamoDB, Cognito, 6 SSM, 3 Lambdas (+ roles, policies, log groups, permissions), API Gateway, ACM, domínio `api-hom`, records DNS, SES; inclui o script `recreate-table.sh` (recria a tabela de hom via `apply -replace`) |
| `environments/prod/` | `gastosapp/prod/terraform.tfstate` | idem, para prod (`api.jrnexpenses.com`, tabela `GastosApp`), sem o script |

Os workflows (`backend-deploy-{hom,prod}.yml`, os 4 dos triggers do
Cognito e os 2 de testes integrados) publicam só via `aws lambda
update-function-code`/`update-function-configuration`, com
`FUNCTION_NAME`/`*_FUNCTION_NAME`/`CICD_ROLE_ARN` cadastrados à mão nos
GitHub Environments `backend-hom`/`backend-prod`. **Nenhum valor de
`terraform output` é consumido por CI** — a extração não muda nada nos
workflows nem nos GitHub Environments.

### O que a FEAT-34 (frontend) já entregou e esta feature herda

A feature irmã `frontend/specs/FEAT-34-extracao-infra-repo-apartado/`
foi concluída em 2026-09-12 (PR
[#121](https://github.com/jrneto/meus-gastos-pessoais/pull/121) no
monorepo, PR [#1](https://github.com/jrneto/infra-jrnexpenses/pull/1)
`develop → main` no repositório novo). Estado herdado:

- **Repositório `infra-jrnexpenses` existe** (clone local em
  `D:\git_jrneto\infra-jrnexpenses`, branches `develop` + `main`), com
  `CLAUDE.md` (governança, mapa de states, ordem de dependência, guardrail
  IAM, fluxo de branches), `README.md`, `.gitignore`,
  `scripts/migration/wave-{2,3,4}-*.sh` e `terraform/{cicd/frontend,
  dns, environments/{hom,prod}}`. A etapa 0 ("esqueleto") da spec
  original desta feature **já está feita**.
- **Um state por ambiente já materializado**:
  `infra-jrnexpenses/{hom,prod}/terraform.tfstate` contêm OAC,
  CloudFront, ACM e WAF do frontend. Os `.tf` do frontend usam prefixo
  `frontend-` (`frontend-acm.tf`, `frontend-cloudfront.tf`,
  `frontend-data.tf`, `frontend-outputs.tf`, `frontend-waf.tf`);
  `versions.tf` e `variables.tf` são compartilhados. Variáveis já
  declaradas lá: `aws_region`, `hom_domain_name`/`prod_domain_name`,
  `frontend_bucket_name` — nenhuma colide com as do backend
  (`table_name`, `frontend_origins`); `aws_region` é a única em comum e
  já existe, não se redeclara.
- **`terraform/cicd/frontend/` está lá; `terraform/cicd/backend/` e
  `terraform/bootstrap/` ainda não** — o `CLAUDE.md` do repositório novo
  já os reserva para esta feature.
- **Padrão de referência infra → monorepo validado**: a infra nunca lê
  state do monorepo; quando precisa de um valor do lado do monorepo, usa
  **data source por nome direto na AWS** (`data "aws_s3_bucket"` em
  `frontend-data.tf`). Foi escolhido em vez de string literal
  determinística porque o formato devolvido pelo provider pode variar
  entre versões e gerar `update` in-place indesejado. Esta feature segue
  o mesmo princípio para as Lambdas (ver decisões abaixo).
- **Padrão de referência monorepo → infra validado**:
  `remote_state.tf` no monorepo lendo outputs do state
  `infra-jrnexpenses/{env}/terraform.tfstate`.
- **Docs raiz já parcialmente atualizados**: `/CLAUDE.md` e
  `/docs/architecture.md` já descrevem a divisão plataforma × workload,
  mas com a ressalva "hoje só a parte do frontend migrou; a do backend
  segue no monorepo até uma feature futura" — ressalva que esta feature
  remove. `frontend/infra/CLAUDE.md` tem um parágrafo "Pendente"
  apontando para a FEAT-40, a remover no fechamento.
- **States órfãos deixados no bucket, por decisão do usuário**
  (`gastosapp-frontend/dns/terraform.tfstate`, só `data.*`;
  `gastosapp-frontend/cicd/terraform.tfstate`, vazio) — a FEAT-34
  combinou que a **limpeza única dos órfãos dos dois contextos** acontece
  ao final desta feature, com aprovação (ver `plan.md` da FEAT-34, §7.3).
- **Lições de execução** (registradas na seção "Status" da spec da
  FEAT-34), que viram restrições aqui:
  - por duas vezes um `state push` aprovado foi executado na pasta/arquivo
    errado (repetindo um push da infra em vez do monorepo); pego pelo
    `state list`/`plan` seguinte, sem `apply` no intervalo, sem impacto —
    mas o state desta feature contém Cognito e DynamoDB de produção, então
    a conferência de "onde estou / o que vou empurrar" passa a ser
    obrigatória antes de cada push;
  - o classificador de automação do Claude Code bloqueia
    `terraform state push`/`apply` via ferramenta de forma inconsistente,
    mesmo com aprovação no chat — parte das execuções precisou ser feita
    manualmente pelo usuário no terminal; prever isso no fluxo;
  - outputs novos só entram no state via `apply` (mesmo sem mudança de
    recurso) — depois de cada push do lado da infra há um `apply` com
    "0 to add, 0 to change, 0 to destroy" só para gravar outputs, que
    também exige aprovação.

### Decisões fechadas com o usuário

- **Fronteira** — vai para `infra-jrnexpenses`: DynamoDB, Cognito (User
  Pool + App Client), Parameter Store (todos os `aws_ssm_parameter`), SES
  (identidade, DKIM, verificação), API Gateway (API, integração, rota,
  stage), domínio customizado `api*` (ACM, validação, domain name, api
  mapping), records DNS de `api*`/SES (e o `data "aws_route53_zone"` que
  eles usam), `cicd/`, `bootstrap/` e o script `recreate-table.sh` de hom
  (acompanha a tabela que ele recria). **Fica no monorepo**: as 3
  `aws_lambda_function` (`api`, `account_trigger`,
  `custom_message_trigger`), suas `aws_iam_role`/`aws_iam_role_policy`,
  seus `aws_cloudwatch_log_group`, os 3 `aws_lambda_permission`
  (`apigateway`, `cognito_invoke_account_trigger`,
  `cognito_invoke_custom_message_trigger`) e o
  `data "aws_caller_identity"` usado só pelas policies.
- **Repositório novo já nasceu limpo** na FEAT-34 (commit inicial, sem
  reescrever histórico) — o histórico dos `.tf` do backend continua
  acessível no monorepo.
- **Governança inalterada**: apply continua manual/local, cada
  `apply`/`state push` é aprovado individualmente pelo usuário, nenhum
  Terraform passa a rodar em CI. No repositório novo, commits direto em
  `develop`, um por etapa, e **PR `develop → main` manual ao fechar a
  feature** (fluxo definido no `CLAUDE.md` de lá).
- **Direção única de dependência: monorepo → infra, nunca o inverso.**
  O monorepo lê o state da infra via `terraform_remote_state` (mesmo
  bucket, mesmo padrão do `remote_state.tf` do frontend). A infra
  referencia as Lambdas do monorepo **sem ler state**: por
  `data "aws_lambda_function"` por nome (mesmo padrão do
  `data "aws_s3_bucket"` da FEAT-34, devolve `arn` e `invoke_arn`
  exatamente como o resource devolvia) ou por ARN determinístico a
  partir do nome — a escolha é do `plan.md`, com o critério único de
  **zero diff** no `lambda_config` do Cognito e na `integration_uri` do
  API Gateway.
- **Um state por ambiente no repositório novo, não por contexto**
  (decisão do usuário em 2026-09-12, no `/plan` da FEAT-34, já
  materializada): os `.tf` do backend entram em
  `terraform/environments/{hom,prod}/` com prefixo `backend-` no nome do
  arquivo (`backend-cognito.tf`, `backend-dynamodb.tf`,
  `backend-outputs.tf`, `backend-data.tf`…), ao lado dos `frontend-*.tf`;
  `versions.tf`/`variables.tf` são compartilhados (esta feature só
  acrescenta variáveis). Continuam separados: `dns/`,
  `cicd/{frontend,backend}` e `bootstrap/`. Os states do monorepo mantêm
  as `key`s atuais (`gastosapp/{hom,prod}/…`) e só perdem os recursos
  movidos.
- **Mecanismo de movimentação**, idêntico ao validado nas etapas 2-4 da
  FEAT-34: `terraform state pull` nos dois lados (com backup) →
  `terraform state mv -state=… -state-out=…` offline numa pasta scratch
  (não chama AWS, não esbarra no guardrail de IAM, cobre recursos não
  importáveis como `aws_ses_domain_identity_verification` e
  `aws_acm_certificate_validation`) → `terraform state push` nos dois
  lados → `plan` = "No changes" em ambos. Cada etapa tem seu script
  `scripts/migration/wave-N-backend-*.sh` no repositório novo (registro
  auditável, não reutilizável). Detalhamento por etapa é escopo do
  `plan.md`.
- **Numeração das etapas continua a da FEAT-34** (que usou 0-4 e chamou
  a parte de docs de "etapa 7"): esta feature é composta pelas etapas
  **5** (`bootstrap/` + `cicd/backend/`, só arquivos), **6** (backend
  hom), **7** (backend prod) e **8** (fechamento: limpeza dos states
  órfãos dos dois contextos, docs, PR `develop → main` do repositório
  novo).

Precedentes de estilo/abordagem para infra sem criação de recurso:
`frontend/specs/FEAT-34-extracao-infra-repo-apartado/` (o mesmo
mecanismo, executado de ponta a ponta),
`frontend/specs/FEAT-07-terraform-import-infra/` (reconciliação sem
mudar comportamento) e
`backend/specs/FEAT-12-terraform-dominio-customizado-api/` (leitura
cross-contexto de recurso de outro state).

## Requisitos de negócio / restrições

- **Nenhum recurso AWS criado, destruído, recriado ou alterado** por esta
  feature — só movimentação de state e reorganização de código Terraform.
  Qualquer `plan` que mostre `create`/`destroy`/`replace`/`update` em um
  recurso movido é bloqueio: investigar e corrigir o código antes de
  qualquer `apply`. Atenção especial ao User Pool do Cognito e à tabela
  DynamoDB de prod: um `replace` acidental perde usuários/dados.
- **Custo zero**: nenhum recurso novo na conta. O único artefato novo na
  AWS são objetos de state no bucket já existente (e, no fechamento, a
  remoção dos objetos órfãos).
- **Cada etapa é atômica e verificável**: um state por etapa; ao final,
  `terraform plan` = "No changes" tanto na configuração da infra quanto
  na do monorepo, e o deploy de hom (`backend-deploy-hom.yml`) continua
  verde sem nenhuma alteração em workflow ou GitHub Environment.
- **Rollback conhecido antes de cada `state push`**: o bucket de state é
  versionado — restaurar a versão anterior do objeto reverte o state; o
  código reverte via git; o backup local do `state pull` fica guardado
  até o `plan` final da etapa. Enquanto um recurso existir nos dois
  states (entre o push da infra e o push do monorepo), nenhum `apply`
  pode rodar em nenhum dos dois lados.
- **Conferência obrigatória antes de cada `state push`** (lição da
  FEAT-34): imediatamente antes de pedir aprovação, apresentar ao usuário
  o diretório absoluto onde o comando vai rodar, a `key` do backend
  configurado nesse diretório, o arquivo de state que será empurrado e o
  resultado de `terraform state list` desse arquivo — o usuário aprova
  esse conjunto, não só "o push". Se a ferramenta bloquear a execução, o
  comando exato é entregue ao usuário para rodar no terminal, e a etapa
  só segue depois de `state list`/`plan` confirmarem o resultado.
- **Nenhuma execução sem aprovação prévia explícita do usuário** — vale
  para `state push`, `apply` (inclusive o de "só outputs"), remoção de
  objetos de state órfãos e qualquer comando que altere state remoto ou
  recurso real. `plan`, `state pull` e `state list` são leitura e podem
  rodar livremente. A lista de endereços a mover em cada etapa é revisada
  pelo usuário antes de executar.
- **Nenhuma mudança de comportamento observável de API** — nenhum
  endpoint, contrato, variável de ambiente, nome de função ou parâmetro
  do Parameter Store muda de nome ou valor.
- **Hom antes de prod**, sempre: a etapa de prod só começa depois da de
  hom estar concluída, validada por deploy verde e smoke manual da API de
  hom, e em janela combinada com o usuário.
- **Guardrail de IAM do perfil `agent-toolkit`** (ver
  `backend/infra/CLAUDE.md`, "Gotchas"): as roles de execução Lambda
  ficam no monorepo, então o `plan` do monorepo continua precisando de um
  profile com leitura de IAM (ou `-refresh=false`), como hoje. As
  configurações da infra não contêm IAM gerenciado (`cicd/backend/`
  continua referência) e devem planejar limpas com `agent-toolkit`.
- **Docs coerentes ao fim**: `backend/infra/CLAUDE.md`,
  `backend/infra/terraform/README.md`, `frontend/infra/CLAUDE.md`
  (parágrafo "Pendente"), `/CLAUDE.md` raiz e `/docs/architecture.md`
  (remover a ressalva "hoje só a parte do frontend migrou"), e
  `CLAUDE.md`/`README.md` do `infra-jrnexpenses` (trocar as menções
  "entra na FEAT-40" pelo estado final) passam a refletir que **toda** a
  plataforma vive em `infra-jrnexpenses` e o monorepo só gerencia o
  workload dos dois contextos.

## Impacto observável externamente

Nenhum. Não há endpoint, contrato de API, variável de ambiente, parâmetro
do Parameter Store, nome de função, URL ou GitHub Environment alterado.
O único efeito visível é onde o código Terraform vive e em qual state
cada recurso está registrado.

## User Stories

**US1 — `bootstrap/` e `cicd/` do backend movidos como arquivos (etapa 5)**
- Given `backend/infra/terraform/bootstrap/` (state local) e
  `backend/infra/terraform/cicd/` (referência, state remoto vazio)
  existem no monorepo, e `infra-jrnexpenses/terraform/cicd/frontend/`
  já existe
- When a etapa 5 é concluída
- Then `bootstrap/` vive em `infra-jrnexpenses/terraform/bootstrap/`
  (com o `terraform.tfstate` local copiado fisicamente, continua
  gitignored) e `cicd/` em `infra-jrnexpenses/terraform/cicd/backend/`,
  as duas pastas foram removidas do monorepo e nenhum state remoto foi
  alterado — o `cicd/` vai **como está**, inclusive o JSON de referência
  desatualizado do README (débito já registrado em
  `backend/docs/backlog.md`)

**US2 — Plataforma de hom movida para `infra-jrnexpenses/terraform/environments/hom/` (etapa 6)**
- Given o state `gastosapp/hom/terraform.tfstate` contém plataforma e
  workload juntos, e `infra-jrnexpenses/hom/terraform.tfstate` já contém
  a plataforma do frontend
- When a etapa 6 é concluída
- Then DynamoDB, Cognito (pool + client), os 6 SSM, SES (identidade,
  DKIM, verificação), API Gateway (api, integração, rota, stage), ACM
  `api-hom`, validação, domain name, api mapping, `data.aws_route53_zone`
  e os records DNS (`api_hom_acm_validation`, `api_hom_a`,
  `ses_verification`, `ses_dkim[0..2]`) estão no state
  `infra-jrnexpenses/hom/terraform.tfstate` ao lado dos recursos do
  frontend, em arquivos `backend-*.tf`; o script `recreate-table.sh`
  vive na mesma pasta com o comentário de caminho atualizado; e o
  monorepo mantém exatamente as 3 Lambdas, 3 roles, 3 policies, 3 log
  groups, 3 permissions e o `data.aws_caller_identity` — com
  `terraform plan` = "No changes" nos dois lados

**US3 — Monorepo de hom lê a plataforma via `terraform_remote_state` (etapa 6)**
- Given as policies das Lambdas, a variável de ambiente
  `DynamoDb__TableName` e as `aws_lambda_permission` de hom
  referenciavam ARNs/nomes de DynamoDB, Cognito, SES e API Gateway do
  mesmo state
- When a etapa 6 é concluída
- Then esses valores vêm de outputs do state da infra via
  `terraform_remote_state`, sem nenhum valor hardcoded novo, e a infra
  não lê nada do state do monorepo — as referências da plataforma às
  Lambdas (`lambda_config` do Cognito, `integration_uri` do API Gateway)
  resolvem por nome da função, com valor idêntico ao atual

**US4 — Plataforma de prod movida, mesmo padrão de hom (etapa 7)**
- Given a etapa 6 está concluída e validada (deploy verde + smoke manual
  em `api-hom.jrnexpenses.com`)
- When a etapa 7 é concluída, em janela combinada com o usuário
- Then os recursos equivalentes de prod (incluindo os 2 SSM de CORS,
  `aws_acm_certificate.api` sem `aws_acm_certificate_validation`, e a
  `aws_lambda_function.api` de prod mantida **sem** bloco
  `environment{}`) estão em `infra-jrnexpenses/prod/terraform.tfstate`,
  com `terraform plan` = "No changes" nos dois lados

**US5 — Nenhuma regressão no deploy e na API**
- Given os workflows de deploy e testes integrados não foram alterados
- When cada etapa de hom/prod é concluída
- Then `backend-deploy-hom.yml` (e, para prod, um deploy de release
  quando houver) roda verde, a API responde normalmente no domínio do
  ambiente e `backend-integration-tests-hom.yml` (sob demanda) passa

**US6 — Nenhuma execução sem aprovação explícita, com conferência de alvo**
- Given qualquer comando que altere state remoto ou recurso real
  (`terraform state push`, `terraform apply`, remoção de objeto de state)
- When esse comando está prestes a ser executado
- Then o usuário vê diretório, `key` do backend, arquivo de state e
  `state list` do que vai ser empurrado (no caso de push), revisa e
  aprova explicitamente antes da execução — nada roda de forma autônoma;
  se a ferramenta bloquear, o usuário roda o comando exato no terminal e
  a verificação pós-execução acontece do mesmo jeito

**US7 — Fechamento: states órfãos limpos, docs coerentes, `main` do repositório novo atualizada (etapa 8)**
- Given as etapas 5-7 estão concluídas e os objetos
  `gastosapp-frontend/dns/terraform.tfstate`,
  `gastosapp-frontend/cicd/terraform.tfstate` e
  `gastosapp-backend/cicd/terraform.tfstate` estão órfãos no bucket
  (vazios ou só `data.*`), enquanto `gastosapp/{hom,prod}/…` e
  `gastosapp-frontend/{hom,prod}/…` continuam em uso pelo monorepo
- When a etapa 8 é concluída
- Then os 3 objetos órfãos foram removidos do bucket (um a um, cada
  remoção aprovada, depois de `state pull` confirmar que estão vazios),
  nenhum state em uso foi tocado; `backend/infra/CLAUDE.md`,
  `backend/infra/terraform/README.md`, `frontend/infra/CLAUDE.md`,
  `/CLAUDE.md` raiz, `/docs/architecture.md` e `CLAUDE.md`/`README.md`
  do `infra-jrnexpenses` descrevem a divisão plataforma × workload
  **completa**, sem ressalva de "parte pendente"; e o PR `develop → main`
  do `infra-jrnexpenses` foi aberto (merge manual pelo usuário)

## Critérios de aceite

- [x] Repositório `infra-jrnexpenses` criado, com `CLAUDE.md`,
      `README.md`, `.gitignore` e `terraform/` — **feito na FEAT-34**
      (etapa 0, compartilhada), nada a refazer aqui
- [x] `bootstrap/` e `cicd/` do backend vivem em
      `infra-jrnexpenses/terraform/{bootstrap,cicd/backend}/`; pastas
      removidas de `backend/infra/terraform/`; nenhum state remoto
      alterado nessa etapa (etapa 5)
- [x] `terraform state list` de `infra-jrnexpenses/terraform/environments/hom/`
      contém todos os recursos de plataforma de hom listados em US2
      (além dos do frontend, movidos pela FEAT-34);
      `terraform state list` de `backend/infra/terraform/environments/hom/`
      contém exatamente Lambdas, roles, policies, log groups,
      permissions e `data.aws_caller_identity` (31 e 16 endereços/
      instâncias, respectivamente — conferido na Task 20/31)
- [x] `terraform plan` em `infra-jrnexpenses/terraform/environments/hom/`
      e em `backend/infra/terraform/environments/hom/` após a etapa 6
      — 0 recursos + só "Changes to Outputs" do lado da infra (apply
      aprovado), e o `plan` do monorepo idêntico à baseline. **Exceção
      aceita explicitamente pelo usuário**: não é "No changes" literal
      dos dois lados — 2 drifts pré-existentes (não introduzidos por
      esta migração) persistem: `aws_lambda_function.api` (env vars de
      versão do CI, já esperado desde a FEAT-14) e
      `aws_cognito_user_pool.main.email_configuration.from_email_address`
      (diff cosmético perpétuo do provider AWS com acentuação/aspas,
      não converge nem depois do `apply`)
- [x] O mesmo para prod, após a etapa 7 (mesma exceção aceita — drift
      perpétuo do Cognito, tabela e User Pool sem qualquer outro diff)
- [x] Monorepo lê ARNs/nomes da plataforma via `terraform_remote_state`
      (outputs `dynamodb_table_name`, `dynamodb_table_arn`,
      `cognito_user_pool_arn`, `ses_domain_identity_arn`,
      `api_gateway_execution_arn`); a infra não contém
      `terraform_remote_state` apontando para state do monorepo e
      resolve as Lambdas por nome (`data "aws_lambda_function"`, zero
      diff em `lambda_config`/`integration_uri`)
- [x] `recreate-table.sh` vive em
      `infra-jrnexpenses/terraform/environments/hom/`, com o comentário
      de uso apontando para o caminho novo; removido do monorepo
- [x] Nenhum recurso AWS criado, destruído, recriado ou alterado —
      confirmado pelos `plan` acima (só `update in-place` nos 2 drifts
      já conhecidos) e pela ausência de `apply` com mudanças
      inesperadas em qualquer etapa
- [x] Nenhuma alteração em `.github/workflows/*` nem nos GitHub
      Environments `backend-hom`/`backend-prod`; `backend-deploy-hom.yml`
      verde após a etapa 6; API de hom respondendo (smoke manual)
- [x] `backend-integration-tests-hom.yml` executado sob demanda e verde
      após a etapa 6 (`backend-integration-tests-prod.yml` também
      rodado, opcional, na etapa 7)
- [x] Nenhum `terraform state push`/`apply`/remoção de state executado
      sem aprovação explícita do usuário no momento da execução, sempre
      precedido da conferência de diretório/`key`/arquivo/`state list`
- [x] Objetos órfãos `gastosapp-frontend/dns/terraform.tfstate` e
      `gastosapp-frontend/cicd/terraform.tfstate` removidos do bucket
      (etapa 8, cada um aprovado, confirmados vazios antes);
      `gastosapp/{hom,prod}/…` e `gastosapp-frontend/{hom,prod}/…`
      intactos. **Divergência do previsto**:
      `gastosapp-backend/cicd/terraform.tfstate` **não** foi removido —
      achado na etapa 8: ao contrário do que a documentação afirmava,
      esse objeto contém `aws_iam_role.backend_cicd` e
      `aws_iam_role_policy.backend_cicd` **gerenciados** (não é órfão);
      ver seção "Status" abaixo
- [x] `backend/infra/CLAUDE.md`, `backend/infra/terraform/README.md`,
      `frontend/infra/CLAUDE.md` (parágrafo "Pendente" removido),
      `/CLAUDE.md` raiz, `/docs/architecture.md` e `CLAUDE.md`/`README.md`
      do `infra-jrnexpenses` atualizados, sem ressalva de "só o frontend
      migrou" (etapa 8)
- [ ] PR `develop → main` do `infra-jrnexpenses` aberto ao fim da
      etapa 8 (merge manual)
- [x] `backend/docs/backlog.md` atualizado: FEAT-40 marcada como concluída

## Status (2026-09-13)

Todas as etapas concluídas: 5 (`bootstrap/`+`cicd/backend/`, só
arquivos), 6 (backend hom) e 7 (backend prod). Nenhum recurso AWS foi
criado, destruído ou recriado em nenhuma etapa — só movimentação de
state Terraform e reorganização de código.

**O que foi movido para `infra-jrnexpenses`** (por ambiente, 24
endereços/26 instâncias cada): `aws_dynamodb_table.gastos_app`,
`aws_cognito_user_pool.main`, `aws_cognito_user_pool_client.spa`, 6-7
`aws_ssm_parameter.*`, 3 recursos SES
(`aws_ses_domain_identity`/`_dkim`/`_identity_verification`), 4 recursos
API Gateway (`aws_apigatewayv2_api`/`_integration`/`_route`/`_stage`),
`aws_acm_certificate` (+ `_validation` só em hom), 2 recursos de domínio
customizado (`aws_apigatewayv2_domain_name`/`_api_mapping`) e 4 records
DNS (`api_a`, `api_acm_validation`, `ses_verification`, `ses_dkim` ×3
instâncias) — mais `terraform/bootstrap/` e `terraform/cicd/backend/`
(etapa 5, como estavam).

**O que ficou no monorepo**: em cada ambiente, as 3 `aws_lambda_function`
(API + os 2 triggers do Cognito), suas 3 `aws_iam_role` + 3
`aws_iam_role_policy` + 3 `aws_cloudwatch_log_group` + 3
`aws_lambda_permission` (workload) e `data.aws_caller_identity.current`
— 15 recursos + 1 data, confirmado via `terraform state list` nos dois
ambientes.

**Drift pré-existente, não introduzido por esta migração** (aceito
explicitamente pelo usuário, ver critérios de aceite acima): (1)
`aws_lambda_function.api` sem os valores de `APP_VERSION`/
`APP_COMMIT_SHA`/`APP_ENVIRONMENT` no `.tf` — já esperado desde a
FEAT-14 (CI publica via `update-function-configuration`, fora do
Terraform); (2) `aws_cognito_user_pool.main.email_configuration.from_email_address`
— a AWS devolve esse atributo normalizado de forma diferente do
literal declarado no `.tf` (encoding MIME/RFC 2047 em hom, remoção de
aspas em prod) sempre que há acentuação ou aspas no nome de exibição;
`apply` não converge, o diff reaparece no próximo `plan` indefinidamente
— comportamento do provider AWS, documentado em
`backend/infra/CLAUDE.md`.

**Achado durante a execução, fora do previsto na spec original**: ao
contrário do que toda a documentação anterior afirmava (guardrail de
IAM bloqueia toda leitura/escrita sobre a role `gastosapp-backend-cicd`,
que estaria sempre fora de state), o objeto
`gastosapp-backend/cicd/terraform.tfstate` **contém**
`aws_iam_role.backend_cicd` e `aws_iam_role_policy.backend_cicd`
gerenciados (serial 5, `terraform_version` 1.15.8) — sem nenhum
registro de quando ou como esse `import`/`apply` aconteceu. Por isso
esse objeto **não** entrou na limpeza de states órfãos da etapa 8 (ele
não é órfão) — decisão do usuário ao ser confrontado com o achado.
Documentado em `backend/infra/CLAUDE.md` e no `CLAUDE.md` do
`infra-jrnexpenses`.

**Outros achados durante a execução**:
- Nem o profile `agent-toolkit` nem um profile `default` inicialmente
  disponível conseguiam ler a IAM role
  `jrnexpenses-account-trigger-lambda-exec` (mesmo guardrail já
  documentado) — os `plan`/`state list` que precisavam disso foram
  rodados pelo usuário localmente com um profile próprio (com leitura
  de IAM de verdade), em vez de pela ferramenta.
- O profile usado pelo usuário expirou uma vez no meio da etapa 7
  (sessão temporária, não SSO) e precisou ser renovado manualmente.
- A baseline da etapa 6 revelou os 2 drifts pré-existentes acima antes
  de qualquer `state mv` — tratado como achado a aceitar, não bloqueio,
  já que nenhum dos dois foi causado pela migração.

**Pendências reais para fechar a FEAT-40**: PR
`FEAT-40-extracao-infra-repo-apartado → develop` do monorepo e PR
`develop → main` do `infra-jrnexpenses` — ambos a abrir na sequência
(tasks 65-66), merge manual pelo usuário.

## Fora do escopo

- A parte do frontend (CloudFront, OAC, ACM, WAF, `dns/`, `cicd/` do
  frontend) — já entregue pela
  `frontend/specs/FEAT-34-extracao-infra-repo-apartado/`
- Qualquer mudança de valor/configuração dos recursos movidos (retenção
  de log, throttling, CORS, política de senha, `callback_urls` do Cognito
  de hom etc.) — só movimentação
- Pipeline de CI para `terraform fmt`/`validate`/`plan` no repositório
  novo — apply continua manual; melhoria já registrada em
  `backend/docs/backlog.md`
- Consolidar mais do que um state por ambiente (ex.: substituir o
  `data "aws_route53_zone"` do backend pela `aws_route53_zone.main` de
  `dns/`, ou mover os records de `api*`/SES para `dns/`, agora que vivem
  no mesmo repo) — melhoria já registrada em `backend/docs/backlog.md`,
  não pré-requisito
- Criar módulos Terraform reutilizáveis ou lógica condicional de
  ambiente — hom e prod continuam como configurações paralelas, como hoje
- Trazer `cicd/` para dentro de state (`import` da role/OIDC) — continua
  bloqueado pelo guardrail de IAM, permanece referência; fusão de
  `cicd/frontend` e `cicd/backend` também fora
- Corrigir o JSON desatualizado da role de CI/CD no README do `cicd/` —
  débito registrado em `backend/docs/backlog.md`, tratado no repositório
  novo depois da migração
- Remover os objetos de state que continuam em uso pelo monorepo
  (`gastosapp/{hom,prod}/…`, `gastosapp-frontend/{hom,prod}/…`) —
  seguem sendo os states de workload
- Preservar histórico git dos `.tf` no repositório novo — decisão: nasce
  limpo (já aplicada na FEAT-34)
- Qualquer alteração em workflows de deploy, GitHub Environments ou no
  fluxo de release
