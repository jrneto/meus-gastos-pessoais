# Tasks — FEAT-14: Esteira de CI/CD (GitHub Actions) para o backend

## Endpoint `/health` (Application + Api)

- [x] 1. Criar `GastosApp.Application/Features/Health/HealthResponse.cs`
      (record `Status`/`Version`/`CommitSha`/`Environment`) — criado em
      `Application/Health/HealthResponse.cs`
- [x] 2. Criar `GastosApp.Application/Features/Health/GetHealthQuery.cs`
      (`IQuery<Result<HealthResponse>>`, sem parâmetros) — criado em
      `Application/Health/Queries/GetHealth/GetHealthQuery.cs`
- [x] 3. Criar `GastosApp.Application/Features/Health/GetHealthQueryHandler.cs`
      lendo `APP_VERSION`/`APP_COMMIT_SHA`/`APP_ENVIRONMENT` de
      `IConfiguration`, com fallback `"local"`/`"unknown"` — mesmo
      arquivo do handler acima
- [x] 4. Registrar o handler no DI (`AddApplicationServices`), se não
      for automático via assembly scanning — automático (Mediator
      source generator via `AddMediator`), nenhuma mudança necessária
- [x] 5. Criar `GastosApp.Api/Endpoints/HealthEndpoints.cs`
      (`MapGet("/health", ...)`, `sender.Send(new GetHealthQuery(), ct)`,
      sem `RequireAuthorization()`)
- [x] 6. Registrar `app.MapHealthEndpoints()` em `Program.cs`
- [x] 7. Adicionar contexto de serialização source-generated do
      `HealthResponse` em `AppJsonSerializerContext` (obrigatório sob
      Native AOT)

## Testes do endpoint `/health`

- [x] 8. Teste unitário de `GetHealthQueryHandler` cobrindo os 3
      cenários: variáveis presentes, ausentes (fallback), e
      `APP_ENVIRONMENT` variando entre `hom`/`prod`/`local`
- [x] 9. Teste de componente de `GET /health` (`GastosApp.ComponentTests`,
      `WebApplicationFactory`) validando: status 200 sem autenticação,
      shape do JSON de resposta, valores refletindo configuração
      injetada no factory

## Contrato

- [x] 10. Rodar `./scripts/export-openapi.sh` para regenerar
       `backend/docs/openapi.json` incluindo `GET /health` — feito com
       credenciais AWS reais, só adição (nenhum endpoint existente
       alterado)
- [x] 11. Conferir `dotnet build` + `dotnet test GastosApp.sln` local
       100% verde antes de seguir para a infra — 123 unitários + 61 de
       componente + 1 de integração, todos verdes

## Terraform — `backend/infra/terraform/cicd/` (IAM Role do backend)

- [x] 12. Criar `backend/infra/terraform/cicd/versions.tf` (mesmo
       padrão de `frontend/infra/terraform/cicd/versions.tf`, backend
       S3 com `key = gastosapp-backend/cicd/terraform.tfstate`)
- [x] 13. Criar `backend/infra/terraform/cicd/variables.tf`
       (`aws_region`, `aws_account_id`, `github_org_repo`,
       `hom_function_name` = `gastos-app-api-hom`, `prod_function_name`
       = `gastos-app-api`)
- [x] 14. Criar `backend/infra/terraform/cicd/oidc.tf` — **`data
       "aws_iam_openid_connect_provider"`** (não `resource`, reaproveita
       o Provider já existente na conta, criado para o frontend)
- [x] 15. Criar `backend/infra/terraform/cicd/iam-role.tf` — Role
       `gastosapp-backend-cicd`, trust policy restrita a
       `repo:jrneto/meus-gastos-pessoais:environment:backend-hom` e
       `:environment:backend-prod`
- [x] 16. Criar `backend/infra/terraform/cicd/iam-policy.tf` — policy
       inline com `lambda:UpdateFunctionCode`,
       `lambda:UpdateFunctionConfiguration`, `lambda:GetFunction`,
       `lambda:GetFunctionConfiguration`, escopada às ARNs de
       `gastos-app-api` e `gastos-app-api-hom`
- [x] 17. Criar `backend/infra/terraform/cicd/outputs.tf`
       (`cicd_role_arn`) — código validado com `terraform fmt`/
       `validate` (sem backend real, `.terraform`/lock gitignorados)
- [x] 18. **Pausar e confirmar com o usuário** antes de
       `terraform init`/`plan`/`apply` reais desta config — aprovação já
       confirmada no `plan.md`; execução feita com credenciais AWS reais
- [x] 19. Rodar `terraform init` + `terraform plan` em
       `backend/infra/terraform/cicd/` — `init` OK (backend S3
       configurado), `plan` **falhou** com `AccessDenied` (ver task 20)
- [x] 20. Rodar `terraform apply` — **não rodou**: `plan` falhou antes,
       com `AccessDenied: iam:ListOpenIDConnectProviders`, confirmando
       o mesmo gap do frontend. Role `gastosapp-backend-cicd` criada
       **manualmente pelo usuário** no console
       (`arn:aws:iam::648443184523:role/gastosapp-backend-cicd`).
       `terraform import` tentado em seguida — também bloqueado
       (`AccessDenied` em `iam:GetRole`/`ListOpenIDConnectProviders`);
       Role fica fora do state, mesma situação da do frontend
- [x] 21. Atualizar `backend/infra/terraform/README.md` documentando a
       config `cicd/` (mesmo padrão da seção "cicd/" do README do
       frontend), incluindo o resultado real do `plan` e o JSON pra
       criação manual

## GitHub Environments (configuração manual, fora do Terraform)

- [x] 22. Criar os Environments `backend-hom` e `backend-prod` no
       repositório — feito pelo usuário via UI do GitHub
- [x] 23. Cadastrar as variáveis `CICD_ROLE_ARN`
       (`arn:aws:iam::648443184523:role/gastosapp-backend-cicd`) e
       `FUNCTION_NAME` (`gastos-app-api-hom` / `gastos-app-api`) em
       cada Environment — feito
- [x] 24. Confirmar que "Allow GitHub Actions to create and approve
       pull requests" já está habilitada — confirmado pelo usuário

## Workflows GitHub Actions do backend

- [x] 25. Criar `.github/workflows/backend-feature-pr.yml` — gatilho
       `push` em `branches: ['FEAT-*']`, `paths: ['backend/**']`; job
       `quality` (`dotnet build` + `dotnet test`); job `open-pr`
       (idempotente, PR branch → `develop`)
- [x] 26. Criar `.github/workflows/backend-deploy-hom.yml` — gatilho
       `push` em `develop`, `paths: ['backend/**']`; job `quality`; job
       `deploy` (`environment: backend-hom`, build via
       `infra/lambda/build.sh`, OIDC via
       `aws-actions/configure-aws-credentials`,
       `update-function-code` + `update-function-configuration` com
       `APP_VERSION=dev-<shortSha>`/`APP_COMMIT_SHA`/`APP_ENVIRONMENT=hom`,
       `aws lambda wait function-updated`)
- [x] 27. No mesmo `backend-deploy-hom.yml`, adicionar job
       `draft-release` (`needs: deploy`) criando/atualizando rascunho
       com tag `backend-vX.Y.Z`, **filtrando `gh release list` por
       `tagName` iniciando com `backend-v`** (tanto para achar a
       última publicada quanto o rascunho pendente a substituir)
- [x] 28. Criar `.github/workflows/backend-deploy-prod.yml` — gatilho
       `release: published`; job `quality` com
       `if: startsWith(github.event.release.tag_name, 'backend-v')` e
       checkout na tag; job `deploy` (`environment: backend-prod`,
       mesmo fluxo de build+publish da task 26, com
       `APP_VERSION=${{ github.event.release.tag_name }}`,
       `APP_ENVIRONMENT=prod`)
- [x] 29. No mesmo `backend-deploy-prod.yml`, adicionar job
       `open-pr-main` (`needs: deploy`), idempotente, PR
       `develop → main`

## Ajuste no workflow já existente do frontend (confirmado no `plan.md`)

- [x] 30. Editar `.github/workflows/frontend-deploy-hom.yml`, job
       `draft-release`: filtrar `gh release list` para considerar só
       `tagName` que **não** comece com `backend-v`, tanto na busca da
       última release publicada quanto na busca de rascunho pendente a
       remover/substituir — inclui `--notes-start-tag` explícito nos
       dois workflows (frontend e backend) pra não misturar changelog
       entre contextos

## Documentação

- [x] 31. Atualizar `backend/infra/CLAUDE.md` — nova seção sobre a
       esteira de CI/CD (deploy automatizado hom/prod, IAM Role via
       OIDC reaproveitado, convenção de tag `backend-v*`), seguindo o
       mesmo padrão de registro já usado para as demais features de
       infra
- [x] 32. Atualizar `backend/CLAUDE.md` — menção ao deploy automatizado
       desde a FEAT-14

## Validação end-to-end

- [x] 33. Validar `backend-feature-pr.yml`: push da própria branch
       `FEAT-14-cicd-github-actions` disparou o workflow, `quality`
       passou, PR #7 aberto automaticamente
       (https://github.com/jrneto/meus-gastos-pessoais/pull/7). Não
       exercitado ainda: idempotência em push subsequente (sem
       duplicar PR)
- [ ] 34. Validar `backend-deploy-hom.yml`: merge em `develop` publica
       `https://api-hom.jrnexpenses.com/health` com o build novo
       (`environment: "hom"`), cria rascunho `backend-v0.0.1` (ou
       patch bump correspondente) — **2 bugs reais encontrados e
       corrigidos** (mesmo padrão de validação ao vivo já visto no
       frontend, FEAT-09):
       1. Job `deploy` → step `Build do artefato (Native AOT)` →
          `Permission denied` ao rodar `./infra/lambda/build.sh`
          (arquivo está `100644` no repo, sem bit de execução — sempre
          foi invocado como `bash infra/lambda/build.sh` no fluxo
          manual, ver README). Corrigido em `771970a`.
       2. Job `deploy` → step `Atualizar variáveis de versão na Lambda
          (hom)` → `aws lambda update-function-configuration` falhou
          (`ParamValidation: Error parsing parameter '--environment'`)
          — o prefixo `Variables=$merged` faz o AWS CLI tentar
          interpretar o valor como *shorthand syntax*, que não entende
          JSON aninhado. Corrigido pra passar JSON puro
          (`{"Variables": {...}}`) via `jq -n --argjson`, nos dois
          workflows (hom e prod).
       Reexecução pendente de confirmação após o 2º fix
- [ ] 35. Validar que o rascunho de teste do frontend (se houver
       algum pendente) não foi afetado pela task 34, e vice-versa
       (confirma o filtro de prefixo das tasks 27/30) — **pendente**
- [ ] 36. Publicar manualmente o rascunho `backend-v*` de teste e
       validar `backend-deploy-prod.yml`: `https://api.jrnexpenses.com/health`
       reflete a tag publicada, PR `develop → main` aberto (ou
       confirmado já existente, sem duplicar) — **pendente**
- [ ] 37. Confirmar isolamento: nenhum deploy de hom afetou a Lambda de
       prod, e vice-versa (comparar `/health` dos dois ambientes) —
       **pendente**
- [ ] 38. Confirmar autenticação AWS via OIDC nos logs do workflow
       (sem `AWS_ACCESS_KEY_ID`/secret de longa duração em nenhum
       step) — **pendente**

## Fechamento

- [x] 39. Atualizar `backend/specs/FEAT-14-cicd-github-actions/spec.md`
       — seção "Status" adicionada, honesta sobre o que está
       implementado/testado localmente vs. pendente de provisionamento
       real e validação ao vivo; nenhum critério de aceite marcado
       `[x]` ainda, porque nenhum foi observado rodando de verdade
