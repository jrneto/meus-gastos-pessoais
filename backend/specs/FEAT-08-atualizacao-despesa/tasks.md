# Tasks: FEAT-08 — Atualização de Despesa

## Domain layer

- [x] 1. Adicionar `Expense.Restore(id, userId, description, amountInCents, category, expenseDate, createdAt)` em `backend/src/GastosApp.Domain/Expenses/Expense.cs`

## Application layer — contratos

- [x] 2. Adicionar `UpdateAsync(string userId, string expenseId, string description, long amountInCents, ExpenseCategory category, DateOnly expenseDate, CancellationToken)` a `IExpenseRepository`

## Application layer — Command, Handler, Result, Validator

- [x] 3. Criar `UpdateExpenseCommand` + `UpdateExpenseCommandHandler` + `UpdateExpenseResult` (com `FromExpense`) em `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommand.cs`, mirror de `RegisterExpenseCommand.cs`: `Handle` chama `IExpenseRepository.UpdateAsync` e mapeia `Expense`/`null` para `Result.Success(UpdateExpenseResult.FromExpense(...))`/`ExpenseErrors.NotFound`
- [x] 4. Criar `UpdateExpenseCommandValidator` em `UpdateExpenseCommandValidator.cs` no mesmo diretório, mirror exato de `RegisterExpenseCommandValidator.cs` (descrição obrigatória ≤200 chars, valor > 0, categoria dentro do enum)

## Infrastructure layer — DynamoDB

- [x] 5. Implementar em `DynamoDbExpenseRepository.UpdateAsync` a Query no `GSI2` por `GSI2PK = ID#{expenseId}` (`Limit = 1`) e a checagem de posse (`PK` retornado deve ser `USER#{userId}`) — retornar `null` se não encontrar ou não bater
- [x] 6. Completar `UpdateAsync` com `GetItemAsync` (usando `PK`/`SK` resolvidos) para obter o item completo (`CreatedAt`) — retornar `null` se o item não existir mais nesse ponto
- [x] 7. Implementar o caminho "data não muda": montar o novo item e persistir via `PutItemAsync` sobrescrevendo a mesma chave
- [x] 8. Implementar o caminho "data muda": montar o novo item com `SK`/`GSI1SK` novos e persistir via `TransactWriteItemsAsync` (`Delete` do item antigo com `ConditionExpression = "attribute_exists(PK)"` + `Put` do item novo)
- [x] 9. Retornar `Expense.Restore(...)` com os dados atualizados e o `CreatedAt` preservado ao final de `UpdateAsync`

## Api layer

- [x] 10. Criar `UpdateExpenseRequest` e adicionar `MapPut("/{id}", UpdateExpense)` em `ExpenseEndpoints.cs`, extraindo `userId` do JWT, montando `UpdateExpenseCommand` e mapeando `Result<UpdateExpenseResult>` via `ToHttpResult(value => Results.Ok(value))`

## Testes unitários

- [x] 11. Criar `GastosApp.UnitTests/Application/UpdateExpenseCommandValidatorTests.cs` cobrindo descrição vazia/ausente/> 200 chars, valor <= 0, categoria fora do enum, e caso válido
- [x] 12. Criar `GastosApp.UnitTests/Application/UpdateExpenseCommandHandlerTests.cs` cobrindo: `UpdateAsync` retorna `Expense` → `Result.Success` com `UpdateExpenseResult` correspondente; retorna `null` → `Result.Failure` com `ErrorType.NotFound`/`not-found`; `Received(1).UpdateAsync(...)` chamado com os argumentos corretos
- [x] 13. Criar `GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryUpdateTests.cs` (mock de `IAmazonDynamoDB` via NSubstitute) cobrindo: Query no GSI2 sem resultado → `null` sem chamar `GetItemAsync`/`PutItemAsync`/`TransactWriteItemsAsync`; item de outro usuário → `null` sem persistir; `GetItemAsync` retorna vazio (corrida) → `null`; data inalterada → `PutItemAsync` chamado com a mesma `SK`, `TransactWriteItemsAsync` não chamado; data alterada → `TransactWriteItemsAsync` chamado com `Delete` (chave antiga) + `Put` (chave nova), `PutItemAsync` não chamado; `CreatedAt` do resultado preservado do item original em ambos os casos

## Testes de componente

- [x] 14. Adicionar em `ExpenseEndpointsTests.cs` o cenário de sucesso: `UpdateAsync` mockado retornando `Expense` → 200 com o corpo atualizado (US1)
- [x] 15. Adicionar cenário sem header de autenticação → 401, `UpdateAsync` **não** chamado (US2)
- [x] 16. Adicionar cenários de validação → 400 sem chamar `UpdateAsync`: descrição vazia, valor <= 0, categoria fora do enum (US3)
- [x] 17. Adicionar cenário `UpdateAsync` mockado retornando `null` → 404 com `type` = `.../not-found` (US4/US5)
- [x] 18. Adicionar smoke test de falha inesperada: `UpdateAsync` lança exceção → 500 (`type` = `.../internal-server-error`)

## Fechamento

- [x] 19. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln`, garantir suíte completa passando (166/166: 114 UnitTests + 1 IntegrationTests + 51 ComponentTests)
- [ ] 20. Smoke manual opcional contra AWS real: registrar despesa via `POST /expenses`, atualizar via `PUT /expenses/{id}` mudando só a categoria (espera 200), depois atualizar mudando a data (espera 200, confirmar via `GET /expenses` que aparece na nova data e não na antiga) — pendente, a critério do usuário
- [x] 21. Atualizar `spec.md`: marcar os critérios de aceite concluídos (`- [x]`) e preencher a seção "Status" com o resumo do que foi implementado, mirror do padrão usado em `FEAT-07/spec.md`
