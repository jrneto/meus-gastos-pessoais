# Tasks: FEAT-07 — Exclusão de Despesa

## Infraestrutura (Terraform)

- [x] 1. Adicionar atributo `GSI2PK` e o índice `GSI2` (hash-only, `projection_type = "KEYS_ONLY"`) em `backend/infra/terraform/dynamodb.tf`

## Application layer — contratos e erros

- [x] 2. Adicionar `DeleteAsync(string userId, string expenseId, CancellationToken)` a `IExpenseRepository`
- [x] 3. Criar `ExpenseErrors` em `backend/src/GastosApp.Application/Expenses/ExpenseErrors.cs` com `NotFound()` (`Error.NotFound("not-found", "Despesa não encontrada.")`)

## Application layer — Command e Handler

- [x] 4. Criar `DeleteExpenseCommand` + `DeleteExpenseCommandHandler` em `backend/src/GastosApp.Application/Expenses/Commands/DeleteExpense/DeleteExpenseCommand.cs`, mirror de `RegisterExpenseCommand.cs`: `Handle` chama `IExpenseRepository.DeleteAsync` e mapeia `true`/`false` para `Result.Success()`/`ExpenseErrors.NotFound()`

## Infrastructure layer — DynamoDB

- [x] 5. Atualizar `DynamoDbExpenseRepository.SaveAsync` para gravar `GSI2PK = ID#{expense.Id}`
- [x] 6. Implementar `DynamoDbExpenseRepository.DeleteAsync`: Query no `GSI2` por `GSI2PK = ID#{expenseId}` (`Limit = 1`), retornar `false` se não encontrar
- [x] 7. Na mesma implementação, adicionar a checagem de posse (comparar `PK` do item encontrado com `USER#{userId}`) — retornar `false` sem chamar `DeleteItemAsync` se não bater
- [x] 8. Completar `DeleteAsync` com `DeleteItemAsync` usando `PK`/`SK` exatos e `ConditionExpression = "attribute_exists(PK)"`, capturando `ConditionalCheckFailedException` e retornando `false`

## Api layer

- [x] 9. Adicionar `MapDelete("/{id}", DeleteExpense)` em `ExpenseEndpoints.cs`, extraindo `userId` do JWT, montando `DeleteExpenseCommand` e mapeando `Result` via `ToHttpResult(() => Results.NoContent())`

## Testes unitários

- [x] 10. Criar `GastosApp.UnitTests/Application/DeleteExpenseCommandHandlerTests.cs` cobrindo: `DeleteAsync` retorna `true` → `Result.Success()`; retorna `false` → `Result.Failure` com `ErrorType.NotFound`/código `not-found`; `Received(1).DeleteAsync(userId, expenseId, ...)` chamado com os argumentos corretos
- [x] 11. Criar `GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryDeleteTests.cs` (mock de `IAmazonDynamoDB` via NSubstitute) cobrindo: Query no GSI2 sem resultado → `false` sem chamar `DeleteItemAsync`; item encontrado com `PK` de outro usuário → `false` sem chamar `DeleteItemAsync`; item do usuário correto → `DeleteItemAsync` chamado com `PK`/`SK` exatos e `ConditionExpression`, retorna `true`; `ConditionalCheckFailedException` → `false` sem propagar exceção

## Testes de componente

- [x] 12. Adicionar em `ExpenseEndpointsTests.cs` o cenário de sucesso: `DeleteAsync` mockado para `true` → 204 sem corpo (US1)
- [x] 13. Adicionar cenário sem header de autenticação → 401, `DeleteAsync` **não** chamado (US2)
- [x] 14. Adicionar cenário `DeleteAsync` mockado para `false` → 404 com `type` = `.../not-found` (US3/US4)
- [x] 15. Adicionar cenário de idempotência: duas chamadas sequenciais ao mesmo `id`, mock retornando `true` na primeira e `false` na segunda → 204 depois 404
- [x] 16. Adicionar smoke test de falha inesperada: `DeleteAsync` lança exceção → 500 (`type` = `.../internal-server-error`)

## Fechamento

- [x] 17. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln`, garantir suíte completa passando (145/145: 1 IntegrationTests + 100 UnitTests + 44 ComponentTests)
- [ ] 18. `terraform plan` em `backend/infra/terraform/` — confirmar que a única mudança é a adição do `GSI2` (sem `force replacement` da tabela); aplicação (`terraform apply`) fica a critério do usuário, fora deste checklist — pendente, a critério do usuário
- [ ] 19. Smoke manual opcional contra AWS real: registrar despesa via `POST /expenses`, confirmar via `GET /expenses`, excluir via `DELETE /expenses/{id}` (espera 204), confirmar que sumiu do `GET /expenses`, tentar excluir de novo (espera 404) — pendente, a critério do usuário
- [x] 20. Atualizar `spec.md`: marcar os critérios de aceite concluídos (`- [x]`) e preencher a seção "Status" com o resumo do que foi implementado, mirror do padrão usado em `FEAT-04/spec.md`
