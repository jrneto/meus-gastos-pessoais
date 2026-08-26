# Tasks: FEAT-24 — Relatórios por período

- [ ] 1. Criar `PeriodCalculator` (`backend/src/GastosApp.Application/Reports/PeriodCalculator.cs`) — `PeriodRange` (record struct) e `Calculate(DateOnly date, string period)` retornando período atual + anterior para `week` (semana ISO via `System.Globalization.ISOWeek`), `month` e `year`

- [ ] 2. Adicionar `Application/PeriodCalculatorTests.cs` (`backend/tests/GastosApp.UnitTests/`) — `week` numa quarta-feira, `week` num domingo (caso de borda `DayOfWeek.Sunday`), `month` em meio de mês, `month` com troca de ano (`date=2026-01-15` → anterior = dez/2025), `year`

- [ ] 3. Criar `GetReportsQuery`, `GetReportsQueryHandler`, `GetReportsResult`, `ReportCategoryItem` e `ReportTopCategory` (`backend/src/GastosApp.Application/Reports/Queries/GetReports/GetReportsQuery.cs`) — duas chamadas a `ITransactionRepository.QueryAsync` (período atual com agrupamento por categoria, período anterior só para o total), `Tipo="despesa"` e `Limit=int.MaxValue` nas duas, `ICategoryRepository.ListAsync(accountId, "despesa")` para nome/orçamento por categoria

- [ ] 4. Criar `GetReportsQueryValidator` (`backend/src/GastosApp.Application/Reports/Queries/GetReports/GetReportsQueryValidator.cs`) — `period` obrigatório em `week`/`month`/`year`, `date` obrigatório no formato `YYYY-MM-DD` (atenção ao bug de `.When()` já corrigido na FEAT-23 — `.When()` só deve guardar a regra `Must`, nunca `NotEmpty()`)

- [ ] 5. Registrar `IValidator<GetReportsQuery>` em `ApplicationServiceCollectionExtensions` (`backend/src/GastosApp.Application/DependencyInjection/`)

- [ ] 6. Adicionar `Application/GetReportsQueryValidatorTests.cs` (`backend/tests/GastosApp.UnitTests/`) — `period` ausente/vazio/valor inválido; `date` ausente/vazia/formato inválido/data de calendário inválida (`2026-02-30`); combinação válida

- [ ] 7. Adicionar `Application/GetReportsQueryHandlerTests.cs` (`backend/tests/GastosApp.UnitTests/`, mock `ITransactionRepository`/`ICategoryRepository`) — `totalCents` somado corretamente; `porCategoria` só com `gastoCents > 0`, ordenada decrescente; `maiorGasto` com `percentualOrcamento` calculado (com e sem orçamento definido); `maiorGasto=null` quando `porCategoria` vazio; `variacaoPercentual` positivo, negativo, `null` (anterior zerado, atual não) e `0` (os dois zerados); filtros passados ao mock (`Tipo="despesa"`, `DateFrom`/`DateTo` por período, `Limit=int.MaxValue`)

- [ ] 8. Atualizar `ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownNine` → `...BeyondTheKnownTen`, incluindo `GetReportsQueryValidator` na lista fechada

- [ ] 9. Criar `ReportEndpoints` (`backend/src/GastosApp.Api/Endpoints/ReportEndpoints.cs`) — grupo `/reports`, `RequireAuthorization()` + `ResolveAccountEndpointFilter`, sem `RoleEndpointFilters.Require`, `GetReportsRequest` bindado via `[AsParameters]`

- [ ] 10. Registrar `app.MapReportEndpoints()` em `Program.cs` (`backend/src/GastosApp.Api/Program.cs`)

- [ ] 11. Adicionar `GetReportsResult`, `ReportCategoryItem`, `ReportTopCategory` e `GetReportsRequest` em `AppJsonSerializerContext` (`backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`)

- [ ] 12. Adicionar `Reports/ReportEndpointsTests.cs` (`backend/tests/GastosApp.ComponentTests/`, mock via `WebApplicationFactory`) cobrindo as 15 user stories do `spec.md`: relatório mensal com dados; relatório semanal (semana ISO); relatório anual; `period` ausente/inválido → 400; `date` ausente/inválida → 400; variação positiva; variação negativa; variação `null` (anterior zerado); ambos os períodos zerados (200 zerado); maior gasto com orçamento; maior gasto sem orçamento; `porCategoria` ordenada sem categorias zeradas; isolamento entre contas; qualquer papel (`Leitura`/`Lancar`/`Total`/`Titular`) recebe 200; 401 sem token

- [ ] 13. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` — suíte completa sem regressão

- [ ] 14. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que só `/reports` foi adicionado a `backend/docs/openapi.json` (sem tocar `/transactions`, `/categories`, `/members` ou `/summary`)

- [ ] 15. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-24-relatorios-por-periodo/spec.md` e preencher a seção "Status" (mesmo padrão da FEAT-23), resumindo o que foi implementado
