# Tasks: FEAT-25 — Exportação CSV de transações

- [x] 1. Criar `TransactionCsvBuilder` e `ExportTransactionRow` (`backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/TransactionCsvBuilder.cs`) — formatter puro: cabeçalho `data;descricao;categoria;tipo;valor;lancadoPor`, delimitador `;`, `valor` em reais com vírgula decimal (`amountInCents / 100m`, sem separador de milhar), escaping RFC 4180 (`;`, `"`, `\n`/`\r`), bytes em UTF-8 com BOM

- [x] 2. Adicionar `Application/Transactions/TransactionCsvBuilderTests.cs` (`backend/tests/GastosApp.UnitTests/`) — lista vazia (só cabeçalho); uma linha com colunas na ordem certa; `valor` (`4590`→`"45,90"`, `100`→`"1,00"`, `500000`→`"5000,00"`); campo com `;` entre aspas; campo com `"` com aspas internas duplicadas; campo com `\n`/`\r` entre aspas; campo sem caractere especial sem aspas; bytes começam com o BOM UTF-8 (`0xEF 0xBB 0xBF`)

- [x] 3. Criar `ExportTransactionsQuery` e `ExportTransactionsQueryHandler` (`backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/ExportTransactionsQuery.cs`) — monta `TransactionQueryFilter` (`Cursor=null`, `Limit=int.MaxValue`), chama `ITransactionRepository.QueryAsync`, `ICategoryRepository.ListAsync(accountId, tipo: null)` para nome de categoria (fallback pro `categoryId` se não encontrada), `CreatedByLabelResolver` com cache por página para `lancadoPor`, e `TransactionCsvBuilder.Build` no retorno (`Result<byte[]>`)

- [x] 4. Criar `ExportTransactionsQueryValidator` (`backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/ExportTransactionsQueryValidator.cs`) — mesmas regras de `GetTransactionsQueryValidator` para `tipo`/`yearMonth`/`dateFrom`/`dateTo`/`minAmountInCents`/`maxAmountInCents` (sem `cursor`/`limit`), atenção ao bug de `.When()` (não deve guardar `NotEmpty()`)

- [x] 5. Registrar `IValidator<ExportTransactionsQuery>` em `ApplicationServiceCollectionExtensions` (`backend/src/GastosApp.Application/DependencyInjection/`)

- [x] 6. Adicionar `Application/Transactions/ExportTransactionsQueryValidatorTests.cs` (`backend/tests/GastosApp.UnitTests/`) — `tipo` fora de `despesa`/`receita`; `yearMonth`/`dateFrom`/`dateTo` fora do formato; `dateFrom` posterior a `dateTo`; `minAmountInCents`/`maxAmountInCents` inválido (`<= 0`) ou invertido (`min > max`); combinação válida sem nenhum filtro; combinação válida com todos os filtros

- [x] 7. Adicionar `Application/Transactions/ExportTransactionsQueryHandlerTests.cs` (`backend/tests/GastosApp.UnitTests/`, mock `ITransactionRepository`/`ICategoryRepository`/`IMembershipRepository`) — filtro passado ao repositório com `Cursor=null`/`Limit=int.MaxValue`/demais campos repassados; `categoria` resolvida pelo nome via `categoryId` (com fallback pro `categoryId` quando a categoria não existe mais); `ICategoryRepository.ListAsync` chamado com `tipo: null`; `lancadoPor` = "Você"/e-mail do membro/"Ex-membro" (mesmos casos de `GetTransactionsQueryHandlerTests`); `IMembershipRepository` chamado no máximo uma vez por `createdByUserId` distinto (cache); página sem itens → CSV só de cabeçalho

- [x] 8. Adicionar `GET /export` em `TransactionEndpoints` (`backend/src/GastosApp.Api/Endpoints/TransactionEndpoints.cs`) — registrada antes de `GET /{id}`, sem `RoleEndpointFilters.Require`, `ExportTransactionsRequest` bindado via `[AsParameters]`, resposta via `Results.File(csv, "text/csv; charset=utf-8", "transacoes.csv")`

- [x] 9. Adicionar `ExportTransactionsRequest` em `AppJsonSerializerContext` (`backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`)

- [x] 10. Atualizar `ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownTen` → `...BeyondTheKnownEleven`, incluindo `ExportTransactionsQueryValidator` na lista fechada

- [x] 11. Adicionar `Transactions/ExportTransactionsEndpointTests.cs` (`backend/tests/GastosApp.ComponentTests/`, mock via `WebApplicationFactory`) cobrindo as 11 user stories do `spec.md`: exportar sem filtro; filtro por `tipo`; filtro por `categoryId` (nome resolvido); filtro por período (`yearMonth`); sem resultado (200, CSV só de cabeçalho); filtro inválido → 400; `valor` em reais com vírgula; escaping de descrição com `;`/`"`; isolamento entre contas; qualquer papel (`Leitura`/`Lancar`/`Total`/`Titular`) recebe 200; 401 sem token — incluindo asserção de `Content-Type`/`Content-Disposition` na resposta

- [x] 12. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` — suíte completa sem regressão

- [x] 13. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que só `GET /transactions/export` foi adicionado a `backend/docs/openapi.json` (sem tocar as demais rotas de `/transactions`)

- [x] 14. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-25-exportacao-csv-transacoes/spec.md` e preencher a seção "Status" (mesmo padrão das FEAT-23/24), resumindo o que foi implementado
