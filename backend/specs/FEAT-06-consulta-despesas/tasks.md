# Tasks: FEAT-06 — Consulta de Despesas

## Application layer — contratos e DTOs

- [x] 1. Criar `ExpenseQueryFilter`, `ExpenseQueryItem` e `ExpenseQueryPage` em `backend/src/GastosApp.Application/Common/Interfaces/` (um record por arquivo), conforme assinaturas do `plan.md`
- [x] 2. Adicionar `QueryAsync(ExpenseQueryFilter, CancellationToken)` a `IExpenseRepository`
- [x] 3. Criar `ExpenseCursorPayload` + `ExpenseCursorCodec` (Encode/TryDecode, Base64Url+JSON, nunca lança) em `backend/src/GastosApp.Application/Common/Cursors/`

## Application layer — Query, Handler, Result, Validator

- [x] 4. Criar `GetExpensesQuery` + `GetExpensesQueryHandler` + `GetExpensesResult`/`ExpenseSummary` (com `FromPage`/`FromQueryItem`) em `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs`, mirror de `RegisterExpenseCommand.cs`
- [x] 5. Criar `GetExpensesQueryValidator` em `GetExpensesQueryValidator.cs` no mesmo diretório: regex de `yearMonth`, validação de `category`/`dateFrom`/`dateTo`/faixa de valor/`limit`/`cursor` conforme `plan.md`

## Infrastructure layer — DynamoDB

- [x] 6. Atualizar `DynamoDbExpenseRepository.SaveAsync` para gravar `SK`/`GSI1SK` com granularidade diária (`TXN#{yyyy-MM-dd}#{id}` / `{yyyy-MM-dd}#{id}`)
- [x] 7. Implementar em `DynamoDbExpenseRepository` a árvore de decisão de índice (GSI1 se `Category` presente, senão tabela base) e montagem do `KeyConditionExpression` para as combinações de `yearMonth`/`dateFrom`/`dateTo` descritas no `plan.md`
- [x] 8. Implementar `FilterExpression` de `MinAmountInCents`/`MaxAmountInCents` na `QueryAsync`
- [x] 9. Implementar o loop de paginação interna (preenche `filter.Limit` mesmo com `FilterExpression`, guard de 25 iterações → `Error.Failure`/exceção mapeada a 500) e a reconstrução do `NextCursor` a partir do último item efetivamente incluído na página
- [x] 10. Integrar `ExpenseCursorCodec` na `QueryAsync` (decodificar `filter.Cursor` para `ExclusiveStartKey` de entrada; codificar o cursor de saída com `Index` + chave do último item)

## Api layer

- [x] 11. Criar `GetExpensesRequest` (`[AsParameters]`) e adicionar `MapGet("/", GetExpenses)` em `ExpenseEndpoints.cs`, extraindo `userId` do JWT e mapeando `Result<GetExpensesResult>` via `ToHttpResult`

## Testes unitários

- [x] 12. Criar `GastosApp.UnitTests/Application/GetExpensesQueryValidatorTests.cs` cobrindo todas as regras do validator (formatos válidos/inválidos, combinações consistentes/inconsistentes, `cursor` corrompido, todos os filtros ausentes = válido)
- [x] 13. Criar `GastosApp.UnitTests/Application/GetExpensesQueryHandlerTests.cs` cobrindo montagem do `ExpenseQueryFilter` (incluindo default de `Limit=20` e parse de `category`/datas) e mapeamento `ExpenseQueryPage` → `GetExpensesResult`
- [x] 14. Criar `GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryQueryTests.cs` (mock de `IAmazonDynamoDB` via NSubstitute) cobrindo escolha de índice, `KeyConditionExpression` por combinação de filtros de data, `FilterExpression` de valor, loop de paginação com múltiplas respostas parciais, guard de 25 iterações e encode/decode de cursor

## Testes de componente

- [x] 15. Adicionar em `ExpenseEndpointsTests.cs` os cenários GET sem filtros (US7) e com `yearMonth` (US1), mockando `IExpenseRepository.QueryAsync`
- [x] 16. Adicionar cenários GET com `category` isolado (US2) e `category`+`yearMonth` combinados (US3)
- [x] 17. Adicionar cenários GET com `dateFrom`/`dateTo` (US4) e com faixa de valor `minAmountInCents`/`maxAmountInCents` (US5)
- [x] 18. Adicionar cenário GET com todos os filtros combinados simultaneamente (US6)
- [x] 19. Adicionar cenário de paginação com `cursor` repassado ao repositório (US8) — o percurso completo de múltiplas páginas reais do DynamoDB é coberto pelo teste de unidade do repositório (task 14), já que o Component Test mocka `IExpenseRepository` inteiro
- [x] 20. Adicionar cenário de isolamento entre dois usuários diferentes (US9)
- [x] 21. Adicionar cenário sem token de autenticação → 401 sem chamar o repositório (US10)
- [x] 22. Adicionar `[Theory]`/`[InlineData]` para filtros inconsistentes → 400 com detalhe do campo: `dateFrom > dateTo`, `minAmount > maxAmount`, `yearMonth` malformado, `category` fora do enum, `cursor` inválido (US11)
- [x] 23. Adicionar cenários de robustez: exceção inesperada do repositório → 500; `limit` acima do máximo → 400; `limit` ausente → default 20 repassado ao filtro

## Fechamento

- [x] 24. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln`, garantir suíte completa passando (133/133: 93 UnitTests + 39 ComponentTests + 1 IntegrationTests)
- [ ] 25. Smoke manual opcional contra AWS real: registrar despesas via `POST /expenses` em meses/categorias diferentes e exercitar `GET /expenses` com cada combinação de filtro e paginação — pendente, a critério do usuário
- [x] 26. Atualizar `spec.md`: marcar os critérios de aceite concluídos (`- [x]`) e preencher a seção "Status" com o resumo do que foi implementado, mirror do padrão usado em `FEAT-04/spec.md`
