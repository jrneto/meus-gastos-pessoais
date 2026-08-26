# Tasks — FEAT-23: Resumo mensal (dashboard)

Ordem pensada pra manter dependência antes de dependente (Application →
Api → testes). Sem Domain novo, sem Infrastructure nova — reaproveita
`ITransactionRepository`/`ICategoryRepository` como já existem (ver
`plan.md`). Nenhum recurso AWS novo, nenhum runbook de recriação de
tabela necessário para esta feature.

## Application

- [x] 1. Criar `GetSummaryQuery`+`GetSummaryQueryHandler`+`GetSummaryResult`+`CategorySummaryItem`
      (`backend/src/GastosApp.Application/Summary/Queries/GetSummary/GetSummaryQuery.cs`):
      Handler consulta `ITransactionRepository.QueryAsync` com
      `TransactionQueryFilter(AccountId, Tipo: null, YearMonth: Month,
      CategoryId: null, ..., Limit: int.MaxValue)`, soma
      `receitasCents`/`gastoCents` a partir de `page.Items`, agrupa gasto
      por `CategoryId` em memória; consulta
      `ICategoryRepository.ListAsync(accountId, "despesa")` e filtra só
      categorias com `OrcamentoMensalCents` definido pra montar
      `orcamentoTotalCents` e `porCategoria` (ordenada por `GastoCents`
      decrescente, incluindo categoria com gasto zero); monta
      `ultimosLancamentos` com os 5 primeiros itens de `page.Items` (já
      vem mais recente primeiro), resolvendo `createdByLabel` via
      `CreatedByLabelResolver` (de
      `GastosApp.Application.Transactions.Common`) com cache por request;
      reaproveita `TransactionSummary`/`TransactionSummary.FromQueryItem`
      (de `GastosApp.Application.Transactions.Queries.GetTransactions`)
      sem duplicar o shape.
- [x] 2. Criar `GetSummaryQueryValidator`
      (`backend/src/GastosApp.Application/Summary/Queries/GetSummary/GetSummaryQueryValidator.cs`):
      `Month` obrigatório (`NotEmpty`) e no formato `YYYY-MM`
      (`GeneratedRegex` `^\d{4}-(0[1-9]|1[0-2])$`, mesmo padrão de
      `GetTransactionsQueryValidator.YearMonthRegex`).
- [x] 3. Atualizar `ApplicationServiceCollectionExtensions.AddApplicationServices`
      (`backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`):
      adicionar `services.AddScoped<IValidator<GetSummaryQuery>, GetSummaryQueryValidator>();`.

## Api

- [x] 4. Criar `SummaryEndpoints`
      (`backend/src/GastosApp.Api/Endpoints/SummaryEndpoints.cs`, grupo
      `/summary`): `RequireAuthorization()` +
      `AddEndpointFilter<ResolveAccountEndpointFilter>()`, sem
      `RoleEndpointFilters.Require` (qualquer papel autenticado passa);
      `GetSummary` monta `GetSummaryQuery(currentAccount.AccountId!,
      currentAccount.UserId!, request.Month)` a partir de
      `[AsParameters] GetSummaryRequest` (`string Month = ""`).
- [x] 5. Atualizar `Program.cs`
      (`backend/src/GastosApp.Api/Program.cs`): adicionar
      `app.MapSummaryEndpoints();` junto às demais chamadas
      `Map*Endpoints()`.
- [x] 6. Atualizar `AppJsonSerializerContext.cs`
      (`backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`):
      adicionar `[JsonSerializable(typeof(GetSummaryResult))]`,
      `[JsonSerializable(typeof(CategorySummaryItem))]`,
      `[JsonSerializable(typeof(GetSummaryRequest))]`
      (`TransactionSummary` já está registrado).
- [x] 7. Rodar `dotnet build backend/GastosApp.sln` e corrigir todos os
      erros de compilação antes de seguir para os testes.

## Testes unitários (`backend/tests/GastosApp.UnitTests/`)

- [x] 8. Criar `Application/GetSummaryQueryValidatorTests.cs`: `month`
      ausente/vazio → inválido; formatos inválidos (`2026-13`,
      `2026/08`, `agosto-2026`, `26-08`) → inválido; `2026-08` → válido.
- [x] 9. Criar `Application/GetSummaryQueryHandlerTests.cs` (mock de
      `ITransactionRepository`/`ICategoryRepository`/
      `IMembershipRepository`): `receitasCents`/`gastoCents`/
      `saldoCents` somados corretamente a partir de uma mistura de
      transações `despesa`/`receita` mockadas; `TransactionQueryFilter`
      passado ao mock tem `YearMonth` igual ao `Month` da query,
      `Tipo=null`, `CategoryId=null`, `Limit=int.MaxValue`.
- [x] 10. Completar `Application/GetSummaryQueryHandlerTests.cs`:
      `orcamentoTotalCents` soma só categorias `tipo="despesa"` com
      `orcamentoMensalCents` definido (categoria despesa sem orçamento e
      categoria receita com orçamento não entram); `porCategoria`
      inclui categoria com orçamento e gasto zero no mês (`gastoCents=0`,
      não omitida) e vem ordenada por `gastoCents` decrescente;
      `restanteCents` negativo quando `gastoCents` > `orcamentoTotalCents`
      (sem exceção).
- [x] 11. Completar `Application/GetSummaryQueryHandlerTests.cs`: mês sem
      nenhuma transação (`page.Items` vazio) → todos os totais zerados,
      `porCategoria` ainda reflete categorias com orçamento (gasto
      zero), `ultimosLancamentos` vazio; mais de 5 transações no mês →
      `ultimosLancamentos` traz só as 5 primeiras de `page.Items`; duas
      transações do mesmo `CreatedByUserId` entre as 5 últimas → só uma
      chamada a `FindByAccountAndUserIdAsync` (cache por request, mirror
      do teste equivalente em `GetTransactionsQueryHandlerTests`).

## Testes de componente (`backend/tests/GastosApp.ComponentTests/`)

- [x] 12. Criar `Summary/SummaryEndpointsTests.cs` — parte 1 (mock de
      `ITransactionRepository`/`ICategoryRepository`/
      `IMembershipRepository` via `WebApplicationFactory`, ver FEAT-03):
      `GET /summary?month=YYYY-MM` com dados retorna 200 com
      `saldoCents`/`receitasCents`/`gastoCents`/`orcamentoTotalCents`/
      `restanteCents` batendo com o cenário calculado manualmente
      (números do mockup do dashboard); `month` ausente → 400; `month`
      em formato inválido (`2026-13`) → 400.
- [x] 13. Completar `Summary/SummaryEndpointsTests.cs` — parte 2: mês sem
      nenhuma transação → 200 com tudo zerado; `porCategoria` só com
      despesa + orçamento definido, ordenada por gasto decrescente,
      incluindo categoria com gasto zero; `ultimosLancamentos` limitado
      a 5 itens, ordenados do mais recente ao mais antigo;
      `restanteCents` negativo sem erro quando o gasto ultrapassa o
      orçamento total.
- [x] 14. Completar `Summary/SummaryEndpointsTests.cs` — parte 3:
      isolamento entre contas (dado de uma conta nunca aparece no
      resumo de outra); qualquer papel (`Leitura`/`Lancar`/`Total`/
      `Titular`) recebe 200 em `GET /summary`; 401 sem token.

## Fechamento

- [x] 15. Rodar `dotnet test backend/GastosApp.sln` — suíte completa
      100% passando, sem regressão em `Transactions`/`Categories`/
      `Members`.
- [x] 16. Rodar `./scripts/export-openapi.sh` e conferir
      `backend/docs/openapi.json`: `git diff` deve mostrar só a adição
      do novo `/summary` (`GET`, parâmetro `month`, schema de response,
      `400`/`401`), sem tocar `/transactions`, `/categories` ou
      `/members`; commitar o arquivo atualizado.
- [x] 17. Atualizar `spec.md`: marcar todos os critérios de aceite
      concluídos (`- [x]`) e adicionar a seção "Status" (mesmo padrão de
      `backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`)
      resumindo o que foi implementado.
