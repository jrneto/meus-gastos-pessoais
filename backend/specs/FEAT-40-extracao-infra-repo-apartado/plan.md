# FEAT-40 — Plano técnico: extração da infra Terraform do backend para `infra-jrnexpenses`

Spec: [`spec.md`](spec.md). Feature irmã (concluída):
`frontend/specs/FEAT-34-extracao-infra-repo-apartado/` — este plano
reutiliza o mecanismo de lá (§3) e só detalha as diferenças do backend.

## 1. O que muda, e onde

Não há camada de aplicação envolvida (`backend/src/` não é tocado;
nenhum Command/Handler/DTO, nenhum acesso a DynamoDB muda). As
"camadas" são configurações Terraform raiz em dois repositórios:

| Configuração hoje (monorepo) | Depois | State |
|---|---|---|
| `backend/infra/terraform/bootstrap/` | **movida como arquivos** → `infra-jrnexpenses/terraform/bootstrap/` (+ cópia física do `terraform.tfstate` local, gitignored) | continua local |
| `backend/infra/terraform/cicd/` | **movida como arquivos** → `infra-jrnexpenses/terraform/cicd/backend/` (como está; `versions.tf` mantém a key `gastosapp-backend/cicd/…`, dormente) | fora de state; objeto `gastosapp-backend/cicd/terraform.tfstate` vira órfão → removido na etapa 8 |
| `backend/infra/terraform/environments/hom/` | **dividida**: plataforma → `infra-jrnexpenses/terraform/environments/hom/backend-*.tf` (+ `recreate-table.sh`); ficam `lambda*.tf` | monorepo mantém `gastosapp/hom/…` só com workload; infra `infra-jrnexpenses/hom/…` ganha os 24 endereços |
| `backend/infra/terraform/environments/prod/` | idem → `…/environments/prod/backend-*.tf` | idem, `gastosapp/prod/…` × `infra-jrnexpenses/prod/…` |

### 1.1 Layout final do `infra-jrnexpenses` (o que esta feature acrescenta está marcado com `+`)

```
infra-jrnexpenses/
├── scripts/migration/
│   ├── wave-2-dns.sh … wave-4-frontend-prod.sh
│   ├── + wave-6-backend-hom.sh
│   └── + wave-7-backend-prod.sh
└── terraform/
    ├── + bootstrap/                 # main.tf, outputs.tf, variables.tf, versions.tf, .gitignore (state local, copiado)
    ├── cicd/
    │   ├── frontend/
    │   └── + backend/               # oidc.tf, iam-role.tf, iam-policy.tf, outputs.tf, variables.tf, versions.tf (referência)
    ├── dns/
    └── environments/{hom,prod}/
        ├── versions.tf, variables.tf (+ variáveis do backend)
        ├── frontend-*.tf
        ├── + backend-data.tf        # 3 × data "aws_lambda_function" (por nome)
        ├── + backend-dynamodb.tf, backend-cognito.tf, backend-parameter-store.tf, backend-ses.tf
        ├── + backend-api-gateway.tf, backend-api-gateway-domain.tf, backend-acm.tf, backend-dns.tf
        ├── + backend-outputs.tf
        └── + recreate-table.sh      # só hom
```

### 1.2 Layout final do monorepo (`backend/infra/terraform/`)

```
backend/infra/terraform/
├── .gitignore
├── README.md                        # reescrito: só workload + ponteiros
└── environments/{hom,prod}/
    ├── versions.tf                  # inalterado (key gastosapp/<env>/terraform.tfstate)
    ├── variables.tf                 # aws_region, state_bucket, infra_state_key
    ├── remote_state.tf              # novo
    ├── lambda.tf                    # + aws_lambda_permission.apigateway; refs → local.*
    ├── lambda-account-trigger.tf    # refs → local.*
    └── lambda-custom-message-trigger.tf  # ref → local.*
```

Removidos por ambiente: `acm.tf`, `api-gateway.tf`,
`api-gateway-domain.tf`, `cognito.tf`, `dns.tf`, `dynamodb.tf`,
`ses.tf`, `parameter-store.tf`, `outputs.tf` e (hom)
`recreate-table.sh`. Removidas as pastas `bootstrap/` e `cicd/`.

## 2. Contratos técnicos entre states

### 2.1 Outputs que a infra expõe (`backend-outputs.tf`, por ambiente)

| Output | Valor | Consumidor no monorepo |
|---|---|---|
| `dynamodb_table_name` | `aws_dynamodb_table.gastos_app.name` | env `DynamoDb__TableName` de `aws_lambda_function.api` (só hom) e `.account_trigger` (hom e prod) — **já existe hoje**, mesmo nome |
| `dynamodb_table_arn` | `aws_dynamodb_table.gastos_app.arn` | policies `lambda_exec` (+ `"${arn}/index/*"`) e `account_trigger_lambda_exec` — já existe |
| `cognito_user_pool_arn` | `aws_cognito_user_pool.main.arn` | **novo** — policy `lambda_exec` (`CognitoAccess`) e `source_arn` das 2 `aws_lambda_permission.cognito_invoke_*` |
| `ses_domain_identity_arn` | `aws_ses_domain_identity.main.arn` | policies `lambda_exec` e `account_trigger_lambda_exec` (`SesSendEmail`) — já existe |
| `api_gateway_execution_arn` | `aws_apigatewayv2_api.main.execution_arn` | **novo** — `aws_lambda_permission.apigateway` (`"${…}/*/*"`) |
| `api_gateway_url`, `api_custom_domain_url`, `ses_sender_email` | como hoje | informativos, mantidos com o mesmo nome |

Sem colisão com os outputs do frontend (`cloudfront_*`,
`acm_domain_validation_options`).

### 2.2 O que o monorepo passa a ler (`remote_state.tf`, por ambiente)

```hcl
# mesmo padrão de frontend/infra/terraform/environments/hom/remote_state.tf
data "terraform_remote_state" "infra" {
  backend = "s3"
  config = {
    bucket = var.state_bucket      # gastosapp-terraform-state-648443184523
    key    = var.infra_state_key   # infra-jrnexpenses/<env>/terraform.tfstate
    region = var.aws_region
  }
}

locals {
  dynamodb_table_name       = data.terraform_remote_state.infra.outputs.dynamodb_table_name
  dynamodb_table_arn        = data.terraform_remote_state.infra.outputs.dynamodb_table_arn
  cognito_user_pool_arn     = data.terraform_remote_state.infra.outputs.cognito_user_pool_arn
  ses_domain_identity_arn   = data.terraform_remote_state.infra.outputs.ses_domain_identity_arn
  api_gateway_execution_arn = data.terraform_remote_state.infra.outputs.api_gateway_execution_arn
}
```

Substituições, todas 1-para-1 (o valor resultante é a mesma string ⇒
JSON das policies, env vars e `source_arn` byte a byte iguais ⇒ sem
diff):

| Arquivo | Antes | Depois |
|---|---|---|
| `lambda.tf` | `aws_dynamodb_table.gastos_app.arn` (×2), `aws_cognito_user_pool.main.arn`, `aws_ses_domain_identity.main.arn`, `aws_dynamodb_table.gastos_app.name` (env, só hom) | `local.*` correspondente |
| `lambda.tf` | — | **recebe** `aws_lambda_permission.apigateway` (hoje em `api-gateway.tf`) com `source_arn = "${local.api_gateway_execution_arn}/*/*"` |
| `lambda-account-trigger.tf` | `aws_dynamodb_table.gastos_app.arn`, `.name` (env), `aws_ses_domain_identity.main.arn`, `aws_cognito_user_pool.main.arn` (permission) | `local.*` |
| `lambda-custom-message-trigger.tf` | `aws_cognito_user_pool.main.arn` (permission) | `local.cognito_user_pool_arn` |
| `variables.tf` | `aws_region`, `table_name`, `frontend_origins` | `aws_region`, `state_bucket`, `infra_state_key` (defaults por ambiente, como no frontend) |
| `outputs.tf` | 6 outputs, todos de recursos que saem | **arquivo removido** (nenhum output novo — evita precisar de `apply`, ver §3) |

`data "aws_caller_identity" "current"` fica em `lambda.tf` (usado no
ARN do Parameter Store da policy). `aws_lambda_function.api` de
**prod** não tem bloco `environment{}` — nada a editar ali além das
policies.

### 2.3 O que a infra lê do lado do monorepo: as 3 Lambdas, **sem state** (`backend-data.tf`)

```hcl
# Leitura por nome, mesmo padrão de data "aws_s3_bucket" em frontend-data.tf:
# a infra nunca lê state do monorepo (CLAUDE.md). O data source devolve
# arn/invoke_arn NÃO qualificados (provider >= 4.51), idênticos aos que
# aws_lambda_function.<x>.arn/.invoke_arn devolviam ⇒ zero diff em
# lambda_config (Cognito) e integration_uri (API Gateway).
data "aws_lambda_function" "api"                    { function_name = var.backend_api_function_name }
data "aws_lambda_function" "account_trigger"        { function_name = var.backend_account_trigger_function_name }
data "aws_lambda_function" "custom_message_trigger" { function_name = var.backend_custom_message_trigger_function_name }
```

Usos: `backend-cognito.tf` → `lambda_config { post_confirmation =
data.aws_lambda_function.account_trigger.arn; custom_message =
data.aws_lambda_function.custom_message_trigger.arn }`;
`backend-api-gateway.tf` → `integration_uri =
data.aws_lambda_function.api.invoke_arn`. Exige só `lambda:GetFunction`
(perfil `agent-toolkit` tem). Provider em uso: 5.100.0 (`~> 5.0`).

**Fallback** (só se o `plan` pós-push mostrar diff nesses 2 atributos):
string determinística
`arn:aws:lambda:${var.aws_region}:${account_id}:function:<nome>` e
`arn:aws:apigateway:${var.aws_region}:lambda:path/2015-03-31/functions/<arn>/invocations`
(precisaria de `data "aws_caller_identity"` na infra). Decidido antes
do push, nunca via `apply`.

### 2.4 Variáveis acrescentadas em `variables.tf` da infra (por ambiente)

`table_name` (`GastosApp-Hom` / `GastosApp`), `frontend_origins`
(`["https://hom.jrnexpenses.com"]` /
`["https://jrnexpenses.com","https://www.jrnexpenses.com"]`),
`backend_api_function_name` (`gastos-app-api-hom` / `gastos-app-api`),
`backend_account_trigger_function_name`
(`jrnexpenses-account-trigger-hom` / `jrnexpenses-account-trigger`),
`backend_custom_message_trigger_function_name`
(`jrnexpenses-custom-message-trigger-hom` /
`jrnexpenses-custom-message-trigger`). `aws_region` já existe (não
redeclarar). Nenhuma colisão com
`hom_domain_name`/`domain_name`/`frontend_bucket_name`.

### 2.5 Endereços de state movidos (`mv addr → addr`, nomes lógicos preservados)

**Etapa 6 — `gastosapp/hom/` → `infra-jrnexpenses/hom/` (24 endereços,
26 instâncias):** `aws_dynamodb_table.gastos_app` ·
`aws_cognito_user_pool.main` · `aws_cognito_user_pool_client.spa` ·
`aws_ssm_parameter.{cognito_user_pool_id, cognito_client_id,
cognito_region, cors_hom_origin_0, ses_sender_email,
logging_full_payload_enabled}` · `aws_ses_domain_identity.main` ·
`aws_ses_domain_dkim.main` · `aws_ses_domain_identity_verification.main`
· `aws_apigatewayv2_api.main` · `aws_apigatewayv2_integration.lambda` ·
`aws_apigatewayv2_route.default` · `aws_apigatewayv2_stage.default` ·
`aws_acm_certificate.api_hom` · `aws_acm_certificate_validation.api_hom`
· `aws_apigatewayv2_domain_name.api_hom` ·
`aws_apigatewayv2_api_mapping.api_hom` ·
`aws_route53_record.api_hom_acm_validation` (for_each, 1 instância) ·
`aws_route53_record.api_hom_a` · `aws_route53_record.ses_verification` ·
`aws_route53_record.ses_dkim` (count, 3 instâncias).

**Etapa 7 — `gastosapp/prod/` → `infra-jrnexpenses/prod/` (24
endereços, 26 instâncias):** idem com nomes de prod — 7 SSM
(`cors_production_origin_0`, `cors_production_origin_1` no lugar de
`cors_hom_origin_0`), `aws_acm_certificate.api` **sem**
`aws_acm_certificate_validation`, `aws_apigatewayv2_domain_name.api`,
`aws_apigatewayv2_api_mapping.api`,
`aws_route53_record.api_acm_validation`, `aws_route53_record.api_a`.

Além do `mv`, o script faz `terraform state rm -state=mono.tfstate
data.aws_route53_zone.jrnexpenses` (data source órfão no monorepo; a
infra o relê no próximo `plan`). `data.aws_caller_identity.current`
fica.

**Fica no monorepo, por ambiente (15 recursos + 1 data):**
`aws_lambda_function.{api,account_trigger,custom_message_trigger}`,
`aws_iam_role.{lambda_exec,account_trigger_lambda_exec,custom_message_trigger_lambda_exec}`,
`aws_iam_role_policy.{idem}`,
`aws_cloudwatch_log_group.{lambda,account_trigger_lambda,custom_message_trigger_lambda}`,
`aws_lambda_permission.{apigateway,cognito_invoke_account_trigger,cognito_invoke_custom_message_trigger}`,
`data.aws_caller_identity.current`.

A lista real é sempre conferida contra `terraform state list` no
momento da execução, nunca de memória.

## 3. Mecanismo de movimentação (FEAT-34 §3, com 4 diferenças)

Base: pasta scratch com backend local (`state mv -state/-state-out`
não funciona com backend S3), `state pull` dos dois lados, script
`wave-N-*.sh`, `state push` dos dois lados. Diferenças:

1. **Destino já populado**: `infra.tfstate` vem com os recursos do
   frontend; backup dos **dois** arquivos
   (`backup-<lado>-<env>-<timestamp>.tfstate`) antes do `mv`.
   `state mv` incrementa o serial ⇒ push sem `-force`; `-force` só se
   reclamar de serial **e** o lineage conferir igual ao remoto.
2. **Lado do monorepo: nenhum `apply`, nunca** — o `plan` do monorepo
   tem drift pré-existente nas 3 `aws_lambda_function` (código
   publicado via `update-function-code` + env vars `APP_*` via
   `update-function-configuration`, FEAT-14 plan §1-2, sem
   `ignore_changes`); um `apply` reverteria o código da Lambda para o
   zip local e apagaria as variáveis de versão. Portanto:
   - **Baseline** do monorepo = saída de `terraform plan -no-color`
     salva **antes** da etapa; o critério "No changes" vira: *"o plan
     pós-etapa lista exatamente os mesmos recursos e atributos da
     baseline (só `aws_lambda_function.*`:
     `source_code_hash`/`environment`) e nenhum outro"*. Qualquer
     menção a outro recurso é bloqueio.
   - **Outputs antigos** são removidos **offline** do `mono.tfstate`
     antes do push (`python -c` zerando `outputs` no JSON, preservando
     `serial`/`lineage`; `jq` não está instalado nesta máquina) e
     `outputs.tf` é apagado ⇒ o `plan` não mostra "Changes to Outputs"
     e não há nada a aplicar.
   - `plan` do monorepo roda com o **profile com leitura de IAM** (o
     `agent-toolkit` falha em `iam:GetRole` na role do account-trigger,
     `backend/infra/CLAUDE.md`); `-refresh=false` só como último
     recurso (esconde drift e enfraquece a comparação com a baseline).
3. **Lado da infra: `apply` só de outputs continua obrigatório** (§3.1
   da FEAT-34) e é seguro — não há Lambda nesse state. Sequência: push
   infra → `plan` infra = 0 recursos + "Changes to Outputs" (+8) →
   **`apply` (aprovado)** → `plan` = No changes → `terraform output`
   mostra os 8 valores. Só então o monorepo pode ler via
   `terraform_remote_state` (senão `null` ⇒ o `plan` do monorepo
   mostraria `update` nas policies — bloqueio, não aplicar).
4. **Conferência de alvo antes de cada `push`/`apply`/remoção** (spec,
   lição da FEAT-34): apresentar ao usuário `pwd` absoluto, a `key` em
   `.terraform/terraform.tfstate` do diretório, o caminho do arquivo a
   empurrar e `terraform state list -state=<arquivo>` (contagem +
   lista); o usuário aprova esse conjunto. Se a ferramenta bloquear, o
   comando exato vai para o usuário rodar no terminal; a verificação
   pós-execução (`state list` remoto + `plan`) acontece do mesmo jeito.

Ordem dentro de cada etapa de ambiente: **baseline (plan mono salvo;
plan infra = No changes) → escrever `backend-*.tf` na infra +
`validate` → pull ×2 + backups → `wave-N.sh` (revisado) → `state list`
×2 → push infra (aprov.) → plan infra (só outputs) → apply outputs
infra (aprov.) → plan infra = No changes → `terraform output` → editar
monorepo + zerar outputs offline + `validate` → push mono (aprov.) →
plan mono = igual à baseline → validação (smoke/deploy/testes) →
commits.**

Rollback: `terraform state push -force backup-<lado>-….tfstate` no lado
afetado (ou restaurar a versão anterior do objeto — bucket versionado)
+ `git revert`. Entre o push da infra e o push do monorepo o recurso
está nos dois states: nenhum `apply` nesse intervalo além do de outputs
da infra.

## 4. Etapas

### Etapa 5 — `bootstrap/` + `cicd/backend/` (só arquivos, sem Terraform)
1. Infra: `terraform/bootstrap/` ← `main.tf`, `outputs.tf`,
   `variables.tf`, `versions.tf`, `.gitignore` do monorepo (conteúdo
   intacto; só o comentário de `main.tf` que aponta para
   `backend/infra/terraform/README.md` passa a apontar para o README da
   infra) + **cópia física** de `bootstrap/terraform.tfstate`
   (gitignored lá também). `terraform/cicd/backend/` ← os 6 `.tf`
   **como estão** (key `gastosapp-backend/cicd/…` dormente, mesma
   prática de `cicd/frontend/`).
2. Infra `README.md`: seção "`bootstrap/`" (do §1 do README do
   monorepo) e seção "`cicd/backend/`" (trecho "cicd/ — OIDC
   Provider…" do README do monorepo, com o débito do JSON desatualizado
   citado, não corrigido); layout atualizado. `CLAUDE.md` da infra:
   linha do `bootstrap/` deixa de dizer "(FEAT-40)".
3. Monorepo: remover `backend/infra/terraform/{bootstrap,cicd}/`; no
   README, as duas seções viram ponteiros. `.gitignore` de
   `backend/infra/terraform/` permanece.
4. Commit infra (`develop`) + commit monorepo (branch).

### Etapa 6 — backend hom
1. Pré-condições: 3 zips em `backend/infra/lambda/` (necessários para
   `filebase64sha256` no `plan`); `init` feito em
   `backend/infra/terraform/environments/hom/` e
   `infra-jrnexpenses/terraform/environments/hom/`; baseline `plan` do
   monorepo salvo (profile IAM); `plan` da infra hom = No changes.
2. Infra hom: criar `backend-data.tf`, `backend-dynamodb.tf`,
   `backend-cognito.tf`, `backend-parameter-store.tf`, `backend-ses.tf`,
   `backend-api-gateway.tf` (**sem** `aws_lambda_permission`;
   `integration_uri` via data), `backend-api-gateway-domain.tf`,
   `backend-acm.tf`, `backend-dns.tf`, `backend-outputs.tf` (§2.1),
   variáveis (§2.4), `recreate-table.sh` (comentário de uso com o
   caminho novo e nota de que as Lambdas agora estão em outro state —
   `-replace` continua sendo a forma certa; o nome da tabela não muda,
   então o `terraform_remote_state` do monorepo não gera diff).
   Comentários que citam `lambda.tf`/`frontend/infra/terraform/dns/`
   passam a citar os caminhos novos. `terraform validate`; um `plan`
   aqui **antes do push** mostra "24 to add" — é só leitura, **nunca**
   aplicar nesse estado.
3. Scratch: pull ×2 → backups →
   `scripts/migration/wave-6-backend-hom.sh` (24 `mv` + 1 `state rm`),
   revisado pelo usuário → `state list` ×2 (infra: frontend 4 +
   backend 24; mono: 15 + 1 data).
4. Push infra (aprov. + conferência) → plan infra: 0 recursos, só
   outputs → apply outputs (aprov.) → plan = No changes →
   `terraform output`.
5. Monorepo hom: apagar os 8 `.tf` + `outputs.tf` + `recreate-table.sh`;
   criar `remote_state.tf`; editar `lambda*.tf` e `variables.tf`
   (§2.2); zerar `outputs` no `mono.tfstate` offline; `validate`; push
   mono (aprov. + conferência) → plan (profile IAM) = igual à baseline.
6. Validação: smoke em `https://api-hom.jrnexpenses.com` (rota
   pública/401 esperado numa protegida); `workflow_dispatch` de
   `backend-deploy-hom.yml` → verde (o filtro de paths não cobre
   Terraform, então o deploy não dispara sozinho); `workflow_dispatch`
   de `backend-integration-tests-hom.yml` → verde.
7. Commit infra (`develop`) + commit monorepo (branch).

### Etapa 7 — backend prod
Idem etapa 6 com `wave-7-backend-prod.sh` e nomes de prod (§2.5),
**janela combinada com o usuário**. Cuidados extras: `plan` da infra
pós-push revisado linha a linha em `aws_cognito_user_pool.main`
(usuários; `deletion_protection = ACTIVE` ajuda, mas um `replace`
planejado já é bloqueio) e `aws_dynamodb_table.gastos_app` (dados; sem
deletion protection); `aws_lambda_function.api` de prod não tem
`environment{}` (só policies mudam de referência). Validação: smoke em
`https://api.jrnexpenses.com`; deploy de prod só na próxima Release
real (não forçar); `backend-integration-tests-prod.yml` opcional, a
critério do usuário.

### Etapa 8 — fechamento
1. **States órfãos** (bucket `gastosapp-terraform-state-648443184523`):
   para cada key — `gastosapp-frontend/dns/terraform.tfstate`,
   `gastosapp-frontend/cicd/terraform.tfstate`,
   `gastosapp-backend/cicd/terraform.tfstate` — `head-object` (se não
   existir, nada a fazer), `aws s3 cp s3://…/key -` mostrando
   `resources: []` (ou só `data.*`), aprovação individual, `aws s3 rm`
   (delete marker; versões anteriores continuam retidas pelo
   versionamento do bucket — purga de versões fora do escopo).
   `gastosapp/{hom,prod}/…` e `gastosapp-frontend/{hom,prod}/…` não são
   tocados.
2. **Docs**: `backend/infra/CLAUDE.md` (seção "Estado atual" nos moldes
   do frontend; caminhos de `ses.tf`/`dns.tf`/`api-gateway.tf`/`cicd/`
   → infra; gotcha do profile IAM segue valendo para o monorepo);
   `backend/infra/terraform/README.md` (reescrito: só workload,
   `remote_state`, ponteiros); `frontend/infra/CLAUDE.md` (remove
   "Pendente"); `/CLAUDE.md` (linhas "hoje só a parte do frontend…",
   seção Infraestrutura); `/docs/architecture.md` (§ infra, linhas do
   Cognito/DynamoDB/API GW na tabela, "hoje só frontend");
   `backend/CLAUDE.md` (árvore: `infra/terraform/` = workload);
   `backend/docs/constitution.md` (frase factual "provisionada via
   Terraform em `backend/infra/terraform/`, cobrindo DynamoDB, Cognito
   e Parameter Store" → plataforma em `infra-jrnexpenses`);
   `backend/docs/data-model.md` e
   `backend/tests/GastosApp.IntegrationTests/README.md` (só se citarem
   caminhos movidos — `grep backend/infra/terraform`);
   `infra-jrnexpenses/CLAUDE.md` + `README.md` (remover "entra na
   FEAT-40", tabela de órfãos → "removidos em <data>", seção
   `cicd/backend/` já feita na etapa 5).
3. `backend/docs/backlog.md`: FEAT-40 concluída; `spec.md`: critérios
   marcados + seção "Status".
4. PRs: infra `develop → main` (manual, `gh pr create`); monorepo
   branch → `develop` (**manual via `gh`** — `backend-feature-pr.yml`
   não dispara por `backend/infra/terraform/**`/docs). Merge manual
   pelo usuário.

## 5. Decisões técnicas

- **Um state por ambiente, arquivos `backend-*.tf`** — já decidido e
  materializado (FEAT-34 §5); esta feature só popula.
- **`data "aws_lambda_function"` por nome, não string literal** — mesmo
  raciocínio do `data "aws_s3_bucket"` (valor exatamente igual ao do
  resource; sem `terraform_remote_state` cruzado). Trade-off:
  dependência de leitura em runtime (a função precisa existir — sempre
  existirá enquanto houver API). Fallback literal em §2.3.
- **Nenhum `apply` no monorepo; outputs zerados offline; `outputs.tf`
  removido** — consequência direta do drift aceito na FEAT-14.
  Alternativas descartadas: `apply -target` (não garante atualização de
  outputs e ainda passaria pelo drift), `apply -refresh-only` (gravaria
  o drift no state e mudaria o comportamento de `plan` futuro), manter
  outputs velhos no state (ruído perpétuo de "Changes to Outputs").
- **Baseline por comparação de `plan`**, não por "No changes" absoluto
  — única forma honesta de verificar zero mudança nos recursos movidos
  com o drift pré-existente das Lambdas.
- **`aws_lambda_permission.apigateway` migra para `lambda.tf`** —
  permission é workload (autoriza invocação da função), mesma família
  das 2 permissions do Cognito que já vivem nos arquivos das Lambdas.
- **`locals` em `remote_state.tf`** em vez de
  `data.terraform_remote_state.infra.outputs.x` espalhado — 9 pontos
  de uso; um nome curto por valor deixa o diff dos `lambda*.tf` trivial
  de revisar.
- **`data.aws_route53_zone` removido do state do monorepo via
  `state rm` offline** — sem `apply` no monorepo, o data source órfão
  nunca sairia sozinho.
- **Nomes lógicos preservados** (`api_hom` × `api`, `cors_hom_origin_0`
  × `cors_production_origin_*`) — `mv addr → addr`; unificar é refactor
  futuro via `moved {}`.
- **`recreate-table.sh` acompanha a tabela** — opera via
  `apply -replace` no state onde a tabela está; comentário atualizado,
  comportamento igual.
- **`cicd/backend/` como está, key dormente** — mesma prática do
  `cicd/frontend/`; o objeto órfão sai na etapa 8, o `versions.tf` fica
  como referência.
- **PRs manuais via `gh`** — os workflows de PR automático filtram por
  paths de código; abrir manualmente é mais simples do que ampliar o
  filtro (fora do escopo).

## 6. Recursos AWS usados/afetados

**Nenhum recurso AWS é criado, alterado ou destruído.** Efeitos na
conta: 2 objetos de state da infra reescritos com mais recursos
(`infra-jrnexpenses/{hom,prod}/`), 2 objetos do monorepo reescritos
com menos (`gastosapp/{hom,prod}/`), até 3 objetos órfãos removidos
(delete marker), `.tflock` transitórios, e chamadas de leitura (`plan`,
`data "aws_lambda_function"`, `s3 cp`). Custo: zero. IAM: nada
gerenciado (leitura de IAM só no `plan` do monorepo, como hoje).
Workflows e GitHub Environments: intocados. Nenhum novo parâmetro do
Parameter Store, tabela, índice, App Client ou domínio.

Mapeamento de erros de negócio / contratos de Command/Handler / PK-SK:
não se aplica (feature sem camada de aplicação).

## 7. Decisões confirmadas com o usuário (2026-09-12, no `/plan`)

1. **`plan`/`state push` do monorepo com o profile com leitura de IAM**
   (o mesmo dos applies de IAM das FEAT-33/37) — plan completo, com
   refresh real, para a comparação com a baseline ser honesta.
   `-refresh=false` descartado.
2. **Lambdas referenciadas na infra via `data "aws_lambda_function"`
   por nome** (§2.3); string literal fica só como fallback documentado,
   decidido antes do push se o `plan` mostrar diff.
3. **Nenhum `apply` no monorepo; outputs antigos zerados offline no
   state; `outputs.tf` do monorepo removido**, sem outputs informativos
   novos.
4. **Um PR único ao final, aberto via `gh`** — monorepo (branch →
   `develop`, após a etapa 8) e infra (`develop → main`), mesmo formato
   da FEAT-34 (#121 / #1). Merge manual.
5. **`backend/docs/constitution.md` entra na etapa 8**, só na frase
   factual "provisionada via Terraform em `backend/infra/terraform/`,
   cobrindo DynamoDB, Cognito e Parameter Store" (passa a apontar a
   plataforma para `infra-jrnexpenses`); nenhuma regra imutável muda.
6. **Órfãos removidos com `aws s3 rm` simples** (delete marker; versões
   anteriores ficam retidas pelo versionamento do bucket — purga de
   versões fora do escopo).
7. **Validação de hom via `workflow_dispatch`** de
   `backend-deploy-hom.yml` e `backend-integration-tests-hom.yml`, sem
   push artificial em código.

Nenhum ponto em aberto para o `/tasks`.
