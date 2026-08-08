# Tasks — FEAT-13: Ambiente de homologação do backend

## Código (.NET) — caminho do Parameter Store configurável

- [x] 1. Alterar `AwsParameterStoreExtensions.cs`: remover o `const string ParameterPath`, adicionar parâmetro `path` (default `"/GastosApp/"`) em `AddAwsParameterStore`, usando `path` no lugar da constante em todo o método
- [x] 2. Alterar `Program.cs`: ler `builder.Configuration["ParameterStore:Path"] ?? "/GastosApp/"` e passar para `builder.Configuration.AddAwsParameterStore(parameterStorePath)`
- [x] 3. Conferir/ajustar testes existentes que cobrem `AddAwsParameterStore` para a nova assinatura (parâmetro opcional, comportamento default inalterado) — nenhum teste cobre esse método diretamente hoje, nada a ajustar
- [x] 4. Rodar `dotnet build GastosApp.sln` e `dotnet test GastosApp.sln` — confirmar que nada quebrou (build ok, 180/180 testes passando)

## Terraform — migração de produção para `environments/prod/`

- [x] 5. A partir de `backend/infra/terraform/` (flat): rodar `terraform plan` de baseline — confirmar "No changes." antes de prosseguir — feito via o `.terraform` local existente movido para `environments/prod/` (mesmo efeito: state real comparado antes do `-migrate-state`)
- [x] 6. Criar `backend/infra/terraform/environments/prod/` e mover (`git mv`) os 11 arquivos existentes (`acm.tf`, `api-gateway-domain.tf`, `api-gateway.tf`, `cognito.tf`, `dns.tf`, `dynamodb.tf`, `lambda.tf`, `outputs.tf`, `parameter-store.tf`, `variables.tf`, `versions.tf`) para lá
- [x] 7. Editar `environments/prod/versions.tf`: trocar a `key` do backend S3 para `gastosapp/prod/terraform.tfstate`
- [x] 8. Editar `environments/prod/lambda.tf`: ajustar `filename`/`source_code_hash` para `${path.module}/../../../lambda/function.zip`
- [x] 9. Rodar `terraform init -backend-config=... -migrate-state` em `environments/prod/` (aprovação explícita antes) — `yes` confirmado, state copiado com sucesso
- [x] 10. Rodar `terraform plan` de validação em `environments/prod/` — 1 diff esperado (`filename` da Lambda, mesmo `source_code_hash`) por causa do path novo; aprovado e aplicado (`terraform apply`), plan seguinte deu "No changes."
- [x] 11. Remover a `key` antiga (`gastosapp/terraform.tfstate`) do bucket S3 (aprovação explícita antes) — removida via `aws s3 rm` (bucket versionado, recuperável se necessário)

## Terraform — novo ambiente `environments/hom/`

- [x] 12. Criar `environments/hom/versions.tf` (mesmo backend S3, `key = "gastosapp/hom/terraform.tfstate"`)
- [x] 13. Criar `environments/hom/variables.tf` (`table_name` default `"GastosApp-Hom"`, `frontend_origins` default `[]`)
- [x] 14. Criar `environments/hom/dynamodb.tf` (mesmo modelo de dados de prod: PK/SK, GSI1, GSI2, `PAY_PER_REQUEST`)
- [x] 15. Criar `environments/hom/cognito.tf` (`user-pool-gastos-app-hom`, `controle-gastos-spa-hom`, `callback_urls = ["http://localhost:5173"]`, resto igual a prod)
- [x] 16. Criar `environments/hom/parameter-store.tf` (3 parâmetros em `/GastosApp/Hom/Cognito/*`)
- [x] 17. Criar `environments/hom/lambda.tf` (IAM role/policy e log group com sufixo `-hom`, `function_name = "gastos-app-api-hom"`, mesmo artefato `function.zip`, bloco `environment { variables = { ParameterStore__Path = "/GastosApp/Hom/" } }`)
- [x] 18. Criar `environments/hom/api-gateway.tf` (`name = "gastos-app-api-hom"`, CORS com `var.frontend_origins`, resto igual a prod)
- [x] 19. Criar `environments/hom/acm.tf` (certificado novo `api-hom.jrnexpenses.com`)
- [x] 20. Criar `environments/hom/dns.tf` (`data "aws_route53_zone"`, CNAME de validação, `aws_acm_certificate_validation`, record A alias)
- [x] 21. Criar `environments/hom/api-gateway-domain.tf` (`aws_apigatewayv2_domain_name.api_hom` + `aws_apigatewayv2_api_mapping.api_hom`)
- [x] 22. Criar `environments/hom/outputs.tf` (mesmos 4 outputs de prod, URL apontando para `api-hom.jrnexpenses.com`)

## Terraform — execução de `environments/hom/`

- [x] 23. Rodar `terraform init -backend-config=...` em `environments/hom/` (aprovação explícita antes)
- [x] 24. Rodar `terraform plan` em `environments/hom/` — revisar a lista completa de recursos novos antes de aprovar o `apply` — 21 recursos novos, 0 alterações em produção
- [x] 25. Rodar `terraform apply` em `environments/hom/` (aprovação explícita antes) — 21 recursos criados com sucesso

## Bug encontrado e corrigido durante a validação

- [x] 25a. **Achado**: `/expenses` em hom retornava `500` mesmo após 25 — a Lambda tentava acessar a tabela `GastosApp` (produção) em vez de `GastosApp-Hom`, apesar da variável de ambiente `DynamoDb__TableName` estar corretamente configurada. Causa raiz: `InfrastructureServiceCollectionExtensions.cs` usava `services.Configure<DynamoDbOptions>(configuration.GetSection(...))` — binding via reflection, que **falha silenciosamente sob Native AOT** (mesmo problema já documentado e corrigido para `CognitoOptions` na FEAT-10, mas nunca replicado para `DynamoDbOptions`). Nunca dava problema porque o default hardcoded (`"GastosApp"`) coincidia com o nome real da tabela de produção.
- [x] 25b. Corrigir `InfrastructureServiceCollectionExtensions.cs`: trocar `services.Configure<DynamoDbOptions>(...)` por leitura manual (mesmo padrão de `AddCognitoSdk.cs`), via `services.AddSingleton(_ => Options.Create(new DynamoDbOptions { TableName = section["TableName"] ?? "GastosApp", Region = section["Region"] ?? "us-east-1" }))`
- [x] 25c. Rebuild + testes (`dotnet build`/`dotnet test`, 180/180 passando) e rebuild do artefato Lambda (`bash infra/lambda/build.sh`)
- [x] 25d. Reaplicar o artefato novo em `environments/hom/` e `environments/prod/` (mesmo zip compartilhado, aprovação explícita para cada `apply`) — `terraform plan` final em ambos deu "No changes."

## Validação manual pós-deploy

- [x] 26. `curl -i https://api-hom.jrnexpenses.com/expenses` sem token — confirmado `401`
- [x] 27. Criar usuário de teste temporário via `POST /auth/register` + `admin-confirm-sign-up` no user pool de hom; validar fluxo completo `POST /auth/login` → `GET /auth/me` → `GET /expenses` → `POST /expenses` → `DELETE /expenses/{id}`; confirmado que os dados ficaram na tabela `GastosApp-Hom` (1 item durante o teste, 0 depois da limpeza) e nunca apareceram em `GastosApp` (produção, 16 itens antes/durante/depois, inalterado)
- [x] 28. Excluir o usuário de teste (`admin-delete-user`), sem deixar dado de teste em hom
- [x] 29. `curl -i https://api.jrnexpenses.com/expenses` sem token — confirmado `401`, igual a antes desta feature (zero regressão em produção)

## Documentação

- [x] 30. Atualizar `backend/infra/CLAUDE.md` com a seção do ambiente de homologação (`environments/prod/` + `environments/hom/`, isolamento total, link para esta spec)
- [x] 31. Atualizar `backend/infra/terraform/README.md` (passo a passo por `environments/prod/` e `environments/hom/`, nota sobre artefato de Lambda compartilhado e `ParameterStore__Path` por ambiente)
- [x] 32. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-13-ambiente-homologacao/spec.md` e preencher a seção "Status" (mesmo padrão usado na FEAT-12) resumindo o que foi feito
