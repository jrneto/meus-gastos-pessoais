# Tasks: FEAT-17 — Despesas vinculadas à Categoria dinâmica

## Domain

- [x] 1. Editar `Expense` (`backend/src/GastosApp.Domain/Expenses/Expense.cs`) — `Category` (`ExpenseCategory`) vira `CategoryId` (`string`), `Create`/`Restore` atualizados
- [x] 2. Excluir `backend/src/GastosApp.Domain/Expenses/ExpenseCategory.cs`

## Application — contratos de repositório

- [x] 3. Editar `IExpenseRepository` (`backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs`) — `UpdateAsync` recebe `categoryId` (`string`) no lugar de `category` (`ExpenseCategory`); `ExistsByCategoryAsync` só renomeia o parâmetro (assinatura já era `string`)
- [x] 4. Editar `ExpenseQueryFilter` (`backend/src/GastosApp.Application/Common/Interfaces/ExpenseQueryFilter.cs`) — `Category` (`ExpenseCategory?`) vira `CategoryId` (`string?`)
- [x] 5. Editar `ExpenseQueryItem` (`backend/src/GastosApp.Application/Common/Interfaces/ExpenseQueryItem.cs`) — `Category` (`ExpenseCategory`) vira `CategoryId` (`string`)

## Application — Commands/Queries de despesas

- [x] 6. Editar `RegisterExpenseCommand`+Handler+`RegisterExpenseResult` (`backend/src/GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs`) — `Category` vira `CategoryId` (`string`), Handler sem `Enum.Parse`
- [x] 7. Editar `RegisterExpenseCommandValidator` — injeta `ICategoryRepository`, troca a regra de enum por `MustAsync` confirmando que `CategoryId` existe e pertence ao `UserId` do comando
- [x] 8. Editar `UpdateExpenseCommand`+Handler+`UpdateExpenseResult` (`.../UpdateExpense/UpdateExpenseCommand.cs`) — mesmo rename de `RegisterExpenseCommand`
- [x] 9. Editar `UpdateExpenseCommandValidator` — mesma regra assíncrona de `RegisterExpenseCommandValidator`
- [x] 10. Editar `GetExpensesQuery`+Handler+`GetExpensesResult`+`ExpenseSummary` (`backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs`) — `Category` vira `CategoryId` (`string?`), Handler sem `Enum.Parse`
- [x] 11. Editar `GetExpensesQueryValidator` — remove a regra `BeAValidCategory`/`RuleFor(q => q.Category)` (filtro não valida existência, ver `plan.md`)
- [x] 12. Editar `GetExpenseByIdQuery` se necessário (reaproveita `UpdateExpenseResult` — confirmar que nenhuma referência direta a `ExpenseCategory` sobra no arquivo)

## Application — ajuste na FEAT-16

- [x] 13. Editar `DeleteCategoryCommandHandler` (`backend/src/GastosApp.Application/Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs`) — `ExistsByCategoryAsync` passa a receber `command.CategoryId` em vez de `category.Nome`

## Infrastructure

- [x] 14. Editar `DynamoDbExpenseRepository.SaveAsync` (`backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs`) — atributo `Category`→`CategoryId`, `GSI1PK`/`GSI1SK` usam `expense.CategoryId`
- [x] 15. Editar `DynamoDbExpenseRepository.UpdateAsync` — mesmo rename de atributo/`GSI1PK`, parâmetro `category` (`ExpenseCategory`) vira `categoryId` (`string`)
- [x] 16. Editar `DynamoDbExpenseRepository.GetByIdAsync`/`MapToExpenseQueryItem` — leitura de `CategoryId` direto (sem `Enum.Parse`)
- [x] 17. Editar `DynamoDbExpenseRepository.BuildQueryRequest` — `filter.Category is not null` vira `filter.CategoryId is not null`, `GSI1PK` montado com `filter.CategoryId`
- [x] 18. Confirmar que `DynamoDbExpenseRepository.ExistsByCategoryAsync` (FEAT-16) não precisa de nenhuma mudança de código (só passa a receber um `categoryId` como argumento, mesma assinatura)

## Api

- [x] 19. Editar `ExpenseEndpoints.cs` (`backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs`) — `RegisterExpenseRequest`/`UpdateExpenseRequest`/`GetExpensesRequest`: campo `Category`/`category` renomeado para `CategoryId`/`categoryId`; handlers atualizados
- [x] 20. Rodar `dotnet build backend/GastosApp.sln` e corrigir toda referência solta a `ExpenseCategory` antes de seguir para os testes

## Testes unitários (`backend/tests/GastosApp.UnitTests/`) — editar

- [x] 21. `Domain/ExpenseTests.cs` — trocar `ExpenseCategory.Alimentacao` por `categoryId` de exemplo (string) em todos os casos
- [x] 22. `Application/RegisterExpenseCommandValidatorTests.cs` — trocar caso "categoria fora do enum" por "categoria inexistente"/"de outro usuário" (mock `ICategoryRepository.GetByIdAsync` retornando `null`); caso de categoria própria válida (mock retornando `Category`)
- [x] 23. `Application/UpdateExpenseCommandValidatorTests.cs` — mesmo mirror de `RegisterExpenseCommandValidatorTests`
- [x] 24. `Application/RegisterExpenseCommandHandlerTests.cs` — ajustar asserts de `Category` para `CategoryId`
- [x] 25. `Application/UpdateExpenseCommandHandlerTests.cs` — ajustar asserts de `Category` para `CategoryId`
- [x] 26. `Application/GetExpensesQueryHandlerTests.cs` — filtro por `CategoryId` (string), remover dependência de `ExpenseCategory`
- [x] 27. `Application/GetExpensesQueryValidatorTests.cs` — remover casos de "categoria fora do enum"
- [x] 28. `Application/GetExpenseByIdQueryHandlerTests.cs` — ajustar campo `Category`→`CategoryId`
- [x] 29. `Application/DeleteCategoryCommandHandlerTests.cs` (FEAT-16) — ajustar `Arg.Is`/mocks de `ExistsByCategoryAsync` para usar `categoryId` em vez de nome (`"Alimentacao"`/`"Viagem"`)
- [x] 30. `Infrastructure/DynamoDbExpenseRepositoryDeleteTests.cs` — confirmado sem mudança necessária (não referenciava o atributo de categoria)
- [x] 31. `Infrastructure/DynamoDbExpenseRepositoryGetByIdTests.cs` — trocar fixtures de `Category` (enum) por `CategoryId` (string)
- [x] 32. `Infrastructure/DynamoDbExpenseRepositoryUpdateTests.cs` — trocar fixtures de `Category`/`GSI1PK` esperado para `CategoryId`
- [x] 33. `Infrastructure/DynamoDbExpenseRepositoryQueryTests.cs` — trocar fixtures de filtro/índice para `CategoryId`
- [x] 34. `Infrastructure/DynamoDbExpenseRepositoryExistsByCategoryTests.cs` — confirmado sem mudança necessária (assinatura já era `string`)

## Teste de componente (`backend/tests/GastosApp.ComponentTests/`) — editar

- [x] 35. `Expenses/ExpenseEndpointsTests.cs` — todo payload de request/response troca `category` por `categoryId`; toda criação/atualização bem-sucedida mocka `CategoryRepositoryMock.GetByIdAsync` retornando uma `Category` válida; casos de categoria inválida mockam `null`
- [x] 36. `Categories/CategoryEndpointsTests.cs` (FEAT-16) — cenário `DeleteCategory_ComDespesasAssociadas_Retorna422SemExcluir` mocka `ExpenseRepositoryMock.ExistsByCategoryAsync` com o `categoryId` da própria categoria, não mais um nome

## Fechamento

- [x] 37. Rodar `dotnet test backend/GastosApp.sln` — suíte completa 100% passando (sem regressão em `Categories`)
- [x] 38. Rodar `./scripts/export-openapi.sh` e commitar `backend/docs/openapi.json` atualizado
- [x] 39. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-17-despesas-categoria-dinamica/spec.md` e preencher a seção "Status" com o resumo da implementação
