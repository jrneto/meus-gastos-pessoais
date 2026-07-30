# Tasks — FEAT-11: CORS para o frontend de produção

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a `backend/`, salvo
indicação contrária.

- [x] 1. Ajustar `src/GastosApp.Api/Program.cs`: ler `Cors:ProductionOrigins` além de `Cors:AllowedOrigins` e somar as duas listas (`Concat`) antes de configurar a policy `"Frontend"` (ver snippet em `plan.md`)
- [x] 2. Criar `tests/GastosApp.ComponentTests/Cors/CorsTests.cs`: componente de teste que sobrescreve `Cors:AllowedOrigins`/`Cors:ProductionOrigins` — **desvio do previsto**: `ConfigureAppConfiguration` não funciona aqui, porque o CORS é lido de forma síncrona em `Program.cs` antes do `builder.Build()`, e os provedores de configuração injetados por `ConfigureAppConfiguration` só ficam visíveis depois do `Build()` (funciona para config lida via `IOptions`, como Cognito/DynamoDb, mas não para leitura direta e antecipada como a de CORS); usado `builder.UseSetting(key, value)` em vez disso, que é aplicado cedo o suficiente — e valida:
  - requisição com `Origin` presente em `Cors:AllowedOrigins` recebe `Access-Control-Allow-Origin` correspondente
  - requisição com `Origin` presente em `Cors:ProductionOrigins` recebe `Access-Control-Allow-Origin` correspondente
  - as duas listas coexistem (uma origem de cada uma funciona na mesma execução, sem uma sobrescrever a outra)
  - requisição com `Origin` fora das duas listas não recebe `Access-Control-Allow-Origin`
- [x] 3. Rodar a suíte completa (`dotnet test`) e garantir 100% dos testes passando (critério de conclusão, ver `backend/docs/constitution.md`)
- [x] 4. Ajustar `infra/terraform/variables.tf`: substituir `variable "frontend_origin"` (string) por `variable "frontend_origins"` (list(string)), default com os dois domínios de produção (ver `plan.md`)
- [x] 5. Ajustar `infra/terraform/api-gateway.tf`: `cors_configuration.allow_origins` passa a usar `var.frontend_origins` em vez de `[var.frontend_origin]`
- [x] 6. Acrescentar em `infra/terraform/parameter-store.tf` os 2 novos recursos `aws_ssm_parameter` (`/GastosApp/Cors/ProductionOrigins/0` e `/1`, ver `plan.md`)
- [x] 7. Rodar `terraform plan` em `infra/terraform/` e apresentar o resultado ao usuário — **aguardar aprovação explícita** antes de qualquer `terraform apply` (mesmo padrão da FEAT-09)
- [x] 8. Após aprovação: `terraform apply` (cria os 2 parâmetros SSM, atualiza `cors_configuration` do API Gateway)
- [x] 9. Rebuild da Lambda (`./infra/lambda/build.sh`) para empacotar o `Program.cs` ajustado na task 1
- [x] 10. Rodar `terraform plan` mostrando a atualização do `source_code_hash` da Lambda — **aguardar aprovação explícita** antes do `apply` de redeploy
- [x] 11. Após aprovação: `terraform apply` (redeploy do código da Lambda)
- [x] 12. Validação manual: preflight e requisição real (`curl`) com `Origin: https://jrnexpenses.com` e `Origin: https://www.jrnexpenses.com` contra a API em produção (URL real do API Gateway) — `Access-Control-Allow-Origin` correto nos dois casos, requisição real chegando até a Lambda (401 da própria aplicação); localmente (`dotnet run`), `http://localhost:5173` confirmado sem regressão, origem não permitida sem o header em nenhum dos dois ambientes
- [x] 13. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
