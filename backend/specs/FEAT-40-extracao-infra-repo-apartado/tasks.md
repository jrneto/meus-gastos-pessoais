# FEAT-40 — Tasks

Checklist derivado de [`plan.md`](plan.md) (contratos em §2, mecanismo em
§3, etapas em §4). Legenda: **[infra]** = `D:\git_jrneto\infra-jrnexpenses`
(commits direto em `develop`); **[mono]** = este monorepo, branch
`FEAT-40-extracao-infra-repo-apartado`. **[APROVAÇÃO]** = comando que
altera state remoto ou recurso real (`state push`, `apply`, `aws s3 rm`)
— só roda depois do usuário aprovar explicitamente aquele comando, naquele
momento, **precedido da conferência de alvo** (`pwd` absoluto, `key` em
`.terraform/terraform.tfstate`, arquivo a empurrar, `terraform state list
-state=<arquivo>` com contagem). Se a ferramenta bloquear, o comando exato
vai para o usuário rodar no terminal e a verificação pós-execução acontece
do mesmo jeito. `plan`, `state pull`, `state list`, `state show`, `output`
e `validate` são leitura e não precisam.

Invariantes (plan §3):
- **Lado da infra**: `plan` = "No changes" antes de cada etapa; após o
  push, `plan` = 0 recursos + só "Changes to Outputs"; após o `apply` de
  outputs, "No changes".
- **Lado do monorepo: nenhum `apply`, nunca** (drift pré-existente das 3
  `aws_lambda_function` — FEAT-14). Baseline = saída de `terraform plan
  -no-color` salva antes da etapa (profile com leitura de IAM); critério
  de sucesso pós-etapa = o `plan` lista **exatamente** os mesmos recursos
  e atributos da baseline (só `aws_lambda_function.*`:
  `source_code_hash`/`environment`) e nada mais.
- Qualquer `create`/`destroy`/`replace`/`update` em recurso movido, em
  qualquer `plan`, é bloqueio — parar e investigar. Entre o push da infra
  e o push do monorepo, nenhum `apply` além do de outputs da infra.
- Listas de endereços conferidas contra `terraform state list` real no
  momento da execução, nunca de memória.

## Etapa 5 — `bootstrap/` + `cicd/backend/` (só arquivos, nenhum comando Terraform)

- [x] 1. [infra] Criar `terraform/bootstrap/` com `main.tf`, `outputs.tf`,
      `variables.tf`, `versions.tf` e `.gitignore` copiados de
      `backend/infra/terraform/bootstrap/` (conteúdo intacto; só o
      comentário de `main.tf` que aponta para
      `backend/infra/terraform/README.md` passa a apontar para o README da
      infra) + **cópia física** de `bootstrap/terraform.tfstate` (conferir
      que continua gitignored: `git status` não o lista)
- [x] 2. [infra] Criar `terraform/cicd/backend/` com os 6 `.tf` de
      `backend/infra/terraform/cicd/` (`oidc.tf`, `iam-role.tf`,
      `iam-policy.tf`, `outputs.tf`, `variables.tf`, `versions.tf`) **como
      estão** — key `gastosapp-backend/cicd/terraform.tfstate` mantida,
      dormente, mesma prática de `cicd/frontend/`
- [x] 3. [infra] `README.md`: seção "`bootstrap/`" (a partir do §1 do
      `backend/infra/terraform/README.md`) e seção "`cicd/backend/`"
      (trecho "cicd/ — OIDC Provider…" do README do monorepo, citando o
      débito do JSON desatualizado sem corrigi-lo); layout atualizado.
      `CLAUDE.md`: linha do `bootstrap/` deixa de dizer "(FEAT-40)"
- [x] 4. [infra] Commit em `develop` ("feat(bootstrap,cicd): traz
      bootstrap e cicd/backend do monorepo (FEAT-40 etapa 5)") + push
- [x] 5. [mono] Remover `backend/infra/terraform/{bootstrap,cicd}/`
      (inclusive `.terraform/` locais); no
      `backend/infra/terraform/README.md` as duas seções viram ponteiros
      para o repo novo; `backend/infra/terraform/.gitignore` permanece;
      confirmar que nenhum state remoto foi tocado (nada de Terraform
      rodou); commit

## Etapa 6 — backend hom (`gastosapp/hom/` → `infra-jrnexpenses/hom/`)

- [x] 6. [mono] Pré-condições: os 3 zips existem em `backend/infra/lambda/`
      (necessários ao `filebase64sha256` do `plan`); `terraform init` em
      `backend/infra/terraform/environments/hom/`
- [x] 7. [mono] **Baseline**: `terraform plan -no-color` em
      `environments/hom/` com o profile com leitura de IAM, salvo em
      arquivo fora do repo (ex.: `/tmp/tf-mv/baseline-mono-hom.txt`);
      esperado: só `aws_lambda_function.*` com `source_code_hash`/
      `environment`; `terraform state list` salvo (esperado: 24 endereços
      de plataforma + 15 de workload + `data.aws_caller_identity.current`
      + `data.aws_route53_zone.jrnexpenses`)
- [x] 8. [infra] `terraform init` + `terraform plan` em
      `terraform/environments/hom/` = "No changes" (baseline da infra)
- [x] 9. [infra] `variables.tf` de hom: acrescentar `table_name`
      (`GastosApp-Hom`), `frontend_origins`
      (`["https://hom.jrnexpenses.com"]`), `backend_api_function_name`
      (`gastos-app-api-hom`), `backend_account_trigger_function_name`
      (`jrnexpenses-account-trigger-hom`),
      `backend_custom_message_trigger_function_name`
      (`jrnexpenses-custom-message-trigger-hom`); `aws_region` **não** se
      redeclara (plan §2.4)
- [x] 10. [infra] Criar `backend-data.tf` de hom: 3 ×
      `data "aws_lambda_function"` (`api`, `account_trigger`,
      `custom_message_trigger`) por `function_name` (plan §2.3), com o
      comentário explicando por que não lê state do monorepo
- [x] 11. [infra] Criar `backend-dynamodb.tf`, `backend-parameter-store.tf`
      e `backend-ses.tf` de hom, copiados de `dynamodb.tf`,
      `parameter-store.tf` e `ses.tf` do monorepo sem alteração de
      recurso (nomes lógicos preservados)
- [x] 12. [infra] Criar `backend-cognito.tf` de hom a partir de
      `cognito.tf`, com `lambda_config { post_confirmation =
      data.aws_lambda_function.account_trigger.arn; custom_message =
      data.aws_lambda_function.custom_message_trigger.arn }` (únicas
      linhas alteradas; comentários que citam `lambda*.tf` passam a
      citar o caminho do monorepo)
- [x] 13. [infra] Criar `backend-api-gateway.tf` de hom a partir de
      `api-gateway.tf`: `integration_uri =
      data.aws_lambda_function.api.invoke_arn`; **sem** o
      `aws_lambda_permission.apigateway` (fica no monorepo)
- [x] 14. [infra] Criar `backend-acm.tf`, `backend-api-gateway-domain.tf`
      e `backend-dns.tf` de hom a partir de `acm.tf`,
      `api-gateway-domain.tf` e `dns.tf` (inclui
      `data "aws_route53_zone" "jrnexpenses"`); comentários que citam
      `frontend/infra/terraform/dns/` passam a citar `terraform/dns/`
- [x] 15. [infra] Criar `backend-outputs.tf` de hom com os 8 outputs do
      plan §2.1 (`dynamodb_table_name`, `dynamodb_table_arn`,
      `cognito_user_pool_arn`, `ses_domain_identity_arn`,
      `api_gateway_execution_arn`, `api_gateway_url`,
      `api_custom_domain_url`, `ses_sender_email`)
- [x] 16. [infra] Copiar `recreate-table.sh` para
      `terraform/environments/hom/` com o comentário de uso apontando
      para o caminho novo e nota de que as Lambdas vivem em outro state
      (`apply -replace` continua sendo a forma certa; nome da tabela não
      muda)
- [x] 17. [infra] `terraform validate` em `environments/hom/`; `terraform
      plan` **só leitura** → esperado "24 to add" (**nunca** aplicar
      nesse estado); conferir que os 3 `data.aws_lambda_function`
      resolvem sem erro
- [x] 18. [infra] Criar `scripts/migration/wave-6-backend-hom.sh`: 24 ×
      `terraform state mv -state=mono.tfstate -state-out=infra.tfstate
      "$addr" "$addr"` (lista do plan §2.5 — DynamoDB, User Pool,
      App Client, 6 SSM, 3 SES, 4 API GW, ACM + validação, domain name,
      api mapping, 4 records DNS; endereços sem índice) + `terraform
      state rm -state=mono.tfstate data.aws_route53_zone.jrnexpenses`;
      **usuário revisa a lista** contra o `state list` da task 7
- [x] 19. Preparar scratch (`/tmp/tf-mv`, `versions.tf` só com provider,
      `terraform init`); `state pull` do monorepo hom → `mono.tfstate` +
      `backup-mono-hom-<timestamp>.tfstate`; `state pull` da infra hom →
      `infra.tfstate` + `backup-infra-hom-<timestamp>.tfstate`
- [x] 20. Rodar `wave-6-backend-hom.sh` no scratch; `terraform state list
      -state=infra.tfstate` (4 do frontend + 24 do backend = 28) e
      `-state=mono.tfstate` (15 recursos + `data.aws_caller_identity`);
      conferir `serial` incrementado nos dois arquivos
- [x] 21. [infra] **[APROVAÇÃO]** `terraform state push infra.tfstate` em
      `terraform/environments/hom/` (conferência de alvo antes;
      `-force` só se reclamar de serial **e** lineage conferir);
      `terraform state list` remoto = 28
- [x] 22. [infra] `terraform plan` em `environments/hom/` → esperado
      **0 recursos** e só "Changes to Outputs" (+8); revisar linha a
      linha `aws_cognito_user_pool.main` (`lambda_config`) e
      `aws_apigatewayv2_integration.lambda` (`integration_uri`) — diff
      ali aciona o fallback de ARN literal do plan §2.3 (decidido antes
      de qualquer apply)
- [x] 23. [infra] **[APROVAÇÃO]** `terraform apply` só de outputs (0/0/0);
      `terraform plan` = "No changes"; `terraform output` mostra os 8
      valores (conferir `dynamodb_table_arn` e `cognito_user_pool_arn`
      contra `state show`)
- [x] 24. [mono] `environments/hom/`: apagar `acm.tf`, `api-gateway.tf`,
      `api-gateway-domain.tf`, `cognito.tf`, `dns.tf`, `dynamodb.tf`,
      `ses.tf`, `parameter-store.tf`, `outputs.tf` e `recreate-table.sh`
- [x] 25. [mono] `environments/hom/variables.tf`: fica `aws_region` +
      `state_bucket` (`gastosapp-terraform-state-648443184523`) +
      `infra_state_key` (`infra-jrnexpenses/hom/terraform.tfstate`);
      remove `table_name` e `frontend_origins`. Criar `remote_state.tf`
      (`data "terraform_remote_state" "infra"` + `locals` com os 5
      valores, plan §2.2)
- [x] 26. [mono] `environments/hom/lambda.tf`: trocar
      `aws_dynamodb_table.gastos_app.arn/.name`,
      `aws_cognito_user_pool.main.arn`, `aws_ses_domain_identity.main.arn`
      por `local.*`; **receber** `aws_lambda_permission.apigateway` com
      `source_arn = "${local.api_gateway_execution_arn}/*/*"`;
      `data "aws_caller_identity" "current"` permanece
- [x] 27. [mono] `lambda-account-trigger.tf` e
      `lambda-custom-message-trigger.tf` de hom: mesmas substituições
      1-para-1 por `local.*` (policies, env `DynamoDb__TableName`,
      `source_arn` das permissions)
- [x] 28. [mono] `terraform validate` em `environments/hom/`; `git diff`
      dos `lambda*.tf` revisado — só trocas de referência, nenhum valor
      literal novo
- [x] 29. Zerar `outputs` no `mono.tfstate` offline (`python -c` que
      carrega o JSON, define `outputs = {}` e grava preservando
      `serial`/`lineage` — `jq` não existe nesta máquina); conferir com
      `terraform state list -state=mono.tfstate` que os 16 endereços
      continuam lá
- [x] 30. [mono] **[APROVAÇÃO]** `terraform state push mono.tfstate` em
      `backend/infra/terraform/environments/hom/` (conferência de alvo
      antes — **não** confundir com o push da infra); `terraform state
      list` remoto = 15 + 1 data
- [x] 31. [mono] `terraform plan -no-color` em `environments/hom/` (profile
      IAM) comparado com a baseline da task 7: mesmos recursos e
      atributos (só `aws_lambda_function.*`), sem "Changes to Outputs",
      nenhuma `update` em policies/permissions (se aparecer `null`
      vindo do remote state → bloqueio, revisar task 23). **Nenhum
      `apply`**
- [x] 32. Smoke manual em `https://api-hom.jrnexpenses.com` (rota pública
      responde; rota protegida devolve 401 sem token)
- [x] 33. `workflow_dispatch` de `backend-deploy-hom.yml` → verde
      (`gh workflow run` + `gh run watch`); em seguida
      `workflow_dispatch` de `backend-integration-tests-hom.yml` → verde
- [x] 34. [mono] Atualizar `backend/infra/terraform/README.md` e
      `backend/infra/CLAUDE.md` para hom (monorepo = só workload;
      plataforma no repo novo; `remote_state` lido); commit
      ("infra(backend): hom lê plataforma via remote_state — FEAT-40
      etapa 6")
- [x] 35. [infra] Commit em `develop` ("feat(hom): migra plataforma do
      backend do monorepo (FEAT-40 etapa 6)") com `backend-*.tf`,
      `recreate-table.sh`, `variables.tf`, `wave-6-backend-hom.sh` e
      README (registro do que foi movido); push
- [x] 36. Guardar os backups da task 19 até o fim da etapa 7; anotar no
      README da infra que `gastosapp/hom/` segue em uso (workload)

## Etapa 7 — backend prod (`gastosapp/prod/` → `infra-jrnexpenses/prod/`)

Só começa com a etapa 6 concluída (tasks 31-33 verdes) e **janela
combinada com o usuário**. Cuidado extra: `aws_cognito_user_pool.main`
(usuários; `deletion_protection = ACTIVE`, mas `replace` planejado já é
bloqueio) e `aws_dynamodb_table.gastos_app` (dados; sem deletion
protection).

- [x] 37. [mono] Pré-condições + **baseline** de prod: zips presentes,
      `init`, `terraform plan -no-color` (profile IAM) salvo em
      `baseline-mono-prod.txt`; `state list` salvo (24 + 15 + 2 data)
- [x] 38. [infra] `init` + `plan` em `terraform/environments/prod/` =
      "No changes"
- [x] 39. [infra] `variables.tf` de prod: `table_name` (`GastosApp`),
      `frontend_origins` (`["https://jrnexpenses.com",
      "https://www.jrnexpenses.com"]`), `backend_api_function_name`
      (`gastos-app-api`), `backend_account_trigger_function_name`
      (`jrnexpenses-account-trigger`),
      `backend_custom_message_trigger_function_name`
      (`jrnexpenses-custom-message-trigger`)
- [x] 40. [infra] Criar os `backend-*.tf` de prod (mesmas regras das
      tasks 10-15, nomes lógicos de prod: 7 SSM com
      `cors_production_origin_{0,1}`, `aws_acm_certificate.api` **sem**
      `aws_acm_certificate_validation`, `aws_apigatewayv2_domain_name.api`,
      `aws_apigatewayv2_api_mapping.api`, records `api_acm_validation`/
      `api_a`); sem `recreate-table.sh`
- [x] 41. [infra] `terraform validate`; `plan` só leitura → "24 to add"
      (nunca aplicar)
- [x] 42. [infra] Criar `scripts/migration/wave-7-backend-prod.sh` (24
      `mv` + `state rm data.aws_route53_zone.jrnexpenses`); **usuário
      revisa** contra o `state list` da task 37
- [x] 43. Scratch: `state pull` ×2 (prod) + backups datados
      (`backup-mono-prod-…`, `backup-infra-prod-…`); rodar o script;
      `state list` ×2 (infra 28; mono 15 + 1 data)
- [x] 44. [infra] **[APROVAÇÃO]** `state push infra.tfstate` em
      `terraform/environments/prod/` (conferência de alvo); `plan`
      revisado **linha a linha**: 0 recursos, só outputs (+8);
      User Pool e tabela sem qualquer diff
- [x] 45. [infra] **[APROVAÇÃO]** `apply` só de outputs (0/0/0); `plan` =
      "No changes"; `terraform output` com os 8 valores
- [x] 46. [mono] Editar `environments/prod/` (tasks 24-28 com nomes de
      prod; `infra_state_key` default
      `infra-jrnexpenses/prod/terraform.tfstate`;
      `aws_lambda_function.api` de prod **não** tem `environment{}` —
      só policies e permissions mudam); `terraform validate`
- [x] 47. Zerar `outputs` do `mono.tfstate` de prod offline (task 29)
- [x] 48. [mono] **[APROVAÇÃO]** `state push mono.tfstate` em
      `backend/infra/terraform/environments/prod/` (conferência de
      alvo); `state list` remoto = 15 + 1 data
- [x] 49. [mono] `plan -no-color` (profile IAM) = igual à baseline da
      task 37; **nenhum `apply`**
- [x] 50. Smoke manual em `https://api.jrnexpenses.com` (rota pública +
      401 em protegida); deploy de prod fica para a próxima Release real
      (não forçar); `backend-integration-tests-prod.yml` opcional, a
      critério do usuário
- [x] 51. [mono] `backend/infra/terraform/README.md` e
      `backend/infra/CLAUDE.md` atualizados para prod; commit
      ("infra(backend): prod lê plataforma via remote_state — FEAT-40
      etapa 7")
- [x] 52. [infra] Commit em `develop` ("feat(prod): migra plataforma do
      backend do monorepo (FEAT-40 etapa 7)"); push

## Etapa 8 — fechamento (órfãos, docs, PRs)

- [ ] 53. Para cada key órfã — `gastosapp-frontend/dns/terraform.tfstate`,
      `gastosapp-frontend/cicd/terraform.tfstate`,
      `gastosapp-backend/cicd/terraform.tfstate` — no bucket
      `gastosapp-terraform-state-648443184523`: `aws s3api head-object`
      (se não existir, nada a fazer) e `aws s3 cp s3://…/<key> -`
      mostrando `resources: []` (ou só `data.*`) ao usuário
- [ ] 54. **[APROVAÇÃO]** (uma por objeto) `aws s3 rm s3://…/<key>` para
      cada órfão confirmado vazio; `gastosapp/{hom,prod}/…` e
      `gastosapp-frontend/{hom,prod}/…` **não** são tocados; confirmar
      com `aws s3 ls` depois
- [ ] 55. [mono] Reescrever `backend/infra/terraform/README.md` para o
      estado final (só workload por ambiente, `remote_state`, como rodar
      `init`/`plan` com profile IAM, ponteiros para `infra-jrnexpenses`)
- [ ] 56. [mono] `backend/infra/CLAUDE.md`: seção "Estado atual" nos
      moldes do frontend; caminhos de `ses.tf`/`dns.tf`/
      `api-gateway.tf`/`cicd/` → infra; gotcha do profile IAM segue
      valendo para o monorepo
- [ ] 57. [mono] `frontend/infra/CLAUDE.md`: remover o parágrafo
      "Pendente" que aponta para a FEAT-40
- [ ] 58. [mono] `/CLAUDE.md` raiz (linhas "hoje só a parte do frontend…"
      na intro e na seção Infraestrutura) e `/docs/architecture.md`
      (§ infra, linhas de Cognito/DynamoDB/API GW na tabela, "hoje só
      frontend") passam a descrever a extração completa
- [ ] 59. [mono] `backend/CLAUDE.md` (árvore: `infra/terraform/` =
      workload) e `backend/docs/constitution.md` (só a frase factual
      "provisionada via Terraform em `backend/infra/terraform/`,
      cobrindo DynamoDB, Cognito e Parameter Store" → plataforma em
      `infra-jrnexpenses`; nenhuma regra imutável muda)
- [ ] 60. [mono] `grep -rn "backend/infra/terraform" backend/docs
      backend/tests/GastosApp.IntegrationTests/README.md` — ajustar
      `data-model.md`/README dos testes integrados só se citarem
      caminhos movidos
- [ ] 61. [mono] Conferir que nada em `.github/workflows/*` mudou
      (`git diff develop -- .github/` vazio) e que os GitHub
      Environments `backend-hom`/`backend-prod` não foram tocados
- [ ] 62. [infra] `CLAUDE.md` + `README.md`: remover "entra na FEAT-40",
      tabela de órfãos → "removidos em <data>", mapa de states com os
      24 recursos do backend por ambiente; commit em `develop` + push
- [ ] 63. [mono] Atualizar `spec.md` marcando os critérios de aceite
      concluídos (`- [x]`) e adicionar seção "Status" (data, o que foi
      movido por etapa, órfãos removidos, achados/lições de execução)
- [ ] 64. [mono] `backend/docs/backlog.md`: FEAT-40 marcada como
      concluída; commit ("docs(backend): FEAT-40 concluída — docs finais,
      órfãos removidos")
- [ ] 65. [infra] Abrir PR `develop → main` no `infra-jrnexpenses`
      (`gh pr create`, resumo das etapas 5-7 + órfãos); merge manual
      pelo usuário
- [ ] 66. [mono] Abrir PR `FEAT-40-extracao-infra-repo-apartado → develop`
      manualmente via `gh` (`backend-feature-pr.yml` não dispara por
      `backend/infra/terraform/**`/docs); merge manual pelo usuário

## Testes

Não há código de aplicação nesta feature — `backend/src/` não é tocado,
nenhum Command/Handler/endpoint muda, então não há teste unitário nem de
componente novo; `dotnet test GastosApp.sln` continua obrigatoriamente
verde nos PRs. O "teste" de cada etapa é a tripla já embutida nas tasks:
`plan` da infra = "No changes" após o apply de outputs, `plan` do
monorepo = igual à baseline (sem `apply`), e validação de runtime (smoke
manual + `backend-deploy-hom.yml` + `backend-integration-tests-hom.yml`
via `workflow_dispatch` em hom; smoke em prod).
