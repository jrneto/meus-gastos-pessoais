# Tasks: FEAT-16 — CRUD de Categorias

## Domain

- [x] 1. Criar `Category` (`backend/src/GastosApp.Domain/Categories/Category.cs`) — `Id`/`UserId`/`Nome`/`Cor`/`Icone`/`CreatedAt`, `Create`/`Restore`, mirror de `Expense.cs`
- [x] 2. Criar `CategorySlug` (`backend/src/GastosApp.Domain/Categories/CategorySlug.cs`) — `From(nome)`: trim, lowercase, remove diacríticos, remove caractere especial, colapsa espaços/hífens em `-`

## Application — infraestrutura de erro

- [x] 3. Adicionar `ErrorType.UnprocessableEntity` (`backend/src/GastosApp.Application/Common/Results/ErrorType.cs`) e `Error.UnprocessableEntity(code, message)` (`Error.cs`)
- [x] 4. Mapear `ErrorType.UnprocessableEntity` → 422 em `ResultHttpExtensions.BuildProblem` (`backend/src/GastosApp.Api/Common/ResultHttpExtensions.cs`)
- [x] 5. Criar `CategoryErrors` (`backend/src/GastosApp.Application/Categories/CategoryErrors.cs`) — `NotFound`, `NameConflict`, `CategoryInUse`

## Application — contratos de repositório

- [x] 6. Criar `CategoryWriteOutcome`/`CategoryWriteResult` e `ICategoryRepository` (`backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs`)
- [x] 7. Adicionar `ExistsByCategoryAsync` em `IExpenseRepository` (`backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs`) — só a assinatura, implementação na Infrastructure

## Application — Commands/Queries

- [x] 8. Criar `CreateCategoryCommand`+`CreateCategoryCommandHandler`+`CreateCategoryResult` (`backend/src/GastosApp.Application/Categories/Commands/CreateCategory/CreateCategoryCommand.cs`)
- [x] 9. Criar `CreateCategoryCommandValidator` (`.../CreateCategory/CreateCategoryCommandValidator.cs`) — `Nome` (`NotEmpty`, `MaximumLength(50)`, slug não vazio via `CategorySlug.From`), `Cor` (`NotEmpty`, regex `#RRGGBB`), `Icone` (`NotEmpty`, `MaximumLength(50)`)
- [x] 10. Criar `UpdateCategoryCommand`+`UpdateCategoryCommandHandler`+`UpdateCategoryResult` (`.../UpdateCategory/UpdateCategoryCommand.cs`)
- [x] 11. Criar `UpdateCategoryCommandValidator` (`.../UpdateCategory/UpdateCategoryCommandValidator.cs`) — mesmas regras de `Nome`/`Cor`/`Icone` de `CreateCategoryCommandValidator`
- [x] 12. Criar `DeleteCategoryCommand`+`DeleteCategoryCommandHandler` (`.../DeleteCategory/DeleteCategoryCommand.cs`) — `GetByIdAsync` → checar `ExistsByCategoryAsync` → `DeleteAsync`
- [x] 13. Criar `GetCategoriesQuery`+`GetCategoriesQueryHandler`+`GetCategoriesResult`+`CategorySummary` (`backend/src/GastosApp.Application/Categories/Queries/GetCategories/GetCategoriesQuery.cs`)

## Infrastructure

- [x] 14. Criar `DynamoDbCategoryRepository` (`backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs`) com `CreateAsync` (`PutItem` condicional `attribute_not_exists(PK)`, captura `ConditionalCheckFailedException` → `NameConflict`) e `ListAsync` (`Query` por `PK` + `begins_with(SK, "CAT#")`)
- [x] 15. Implementar `GetByIdAsync` em `DynamoDbCategoryRepository` — `Query` no `GSI2` (`GSI2PK = ID#{id}`) → checagem de posse → `GetItem`, mirror de `DynamoDbExpenseRepository.GetByIdAsync`
- [x] 16. Implementar `DeleteAsync` em `DynamoDbCategoryRepository` — `Query` no `GSI2` → checagem de posse → `DeleteItem` condicional (`attribute_exists(PK)`), mirror de `DynamoDbExpenseRepository.DeleteAsync`
- [x] 17. Implementar `UpdateAsync` em `DynamoDbCategoryRepository` — `Query` no `GSI2` → checagem de posse → slug igual: `PutItem` simples; slug diferente: `TransactWriteItems` (`Delete` condicional do item antigo + `Put` condicional do novo) com inspeção de `TransactionCanceledException.CancellationReasons` para diferenciar `NotFound` de `NameConflict`
- [x] 18. Implementar `ExistsByCategoryAsync` em `DynamoDbExpenseRepository` (`backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs`) — `Query` no `GSI1` (`GSI1PK = USER#{userId}#{category}`, `Limit = 1`)
- [x] 19. Registrar `ICategoryRepository` → `DynamoDbCategoryRepository` em `InfrastructureServiceCollectionExtensions.AddInfrastructure` (`backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`)

## Api

- [x] 20. Criar `CategoryEndpoints.cs` (`backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs`) — `MapGet("/")`, `MapPost("/")`, `MapPut("/{id}")`, `MapDelete("/{id}")` sob `MapGroup("/categories").RequireAuthorization()`, com `CreateCategoryRequest`/`UpdateCategoryRequest`, mirror de `ExpenseEndpoints.cs`
- [x] 21. Registrar os novos DTOs (`CreateCategoryRequest`, `UpdateCategoryRequest`, `CreateCategoryResult`, `UpdateCategoryResult`, `GetCategoriesResult`, `CategorySummary`) em `AppJsonSerializerContext` (`backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`)
- [x] 22. Registrar `app.MapCategoryEndpoints();` em `Program.cs`
- [x] 23. Rodar `dotnet build backend/GastosApp.sln` e corrigir erros de compilação antes de seguir para os testes

## Testes unitários (`backend/tests/GastosApp.UnitTests/`)

- [x] 24. `Domain/CategorySlugTests.cs` — acento, espaços duplos, case, slug vazio (`"!!!"`)
- [x] 25. `Application/CreateCategoryCommandValidatorTests.cs` — nome vazio/> 50 chars/slug vazio, cor fora do formato hex, ícone vazio/> 50 chars
- [x] 26. `Application/UpdateCategoryCommandValidatorTests.cs` — mesmos casos de `CreateCategoryCommandValidatorTests`
- [x] 27. `Application/CreateCategoryCommandHandlerTests.cs` — `Success` → `Result.Success`; `NameConflict` → `Result.Failure` (`UnprocessableEntity`/`name-conflict`)
- [x] 28. `Application/UpdateCategoryCommandHandlerTests.cs` — os três outcomes (`Success`/`NotFound`/`NameConflict`)
- [x] 29. `Application/DeleteCategoryCommandHandlerTests.cs` — não encontrado (sem chamar `ExistsByCategoryAsync`), em uso (sem chamar `DeleteAsync`), caminho feliz completo
- [x] 30. `Application/GetCategoriesQueryHandlerTests.cs` — lista vazia e lista populada
- [x] 31. `Infrastructure/DynamoDbCategoryRepositoryTests.cs` — `CreateAsync` com conflito; `UpdateAsync` sem/com mudança de slug (`PutItem` vs `TransactWriteItems`); `GetByIdAsync`/`DeleteAsync` com item de outro usuário
- [x] 32. `Infrastructure/DynamoDbExpenseRepositoryExistsByCategoryTests.cs` — `Query` no `GSI1` com `GSI1PK` correto

## Teste de componente (`backend/tests/GastosApp.ComponentTests/Categories/CategoryEndpointsTests.cs`)

- [x] 33. Cenários de `GET /categories` — vazio (200, lista vazia) e populado (200)
- [x] 34. Cenários de `POST /categories` — sucesso (201), nome duplicado (422), campos inválidos (400, um caso por campo)
- [x] 35. Cenários de `PUT /categories/{id}` — sucesso (200), nome duplicado (422), inexistente/de outro usuário (404), campos inválidos (400)
- [x] 36. Cenários de `DELETE /categories/{id}` — sucesso (204), com despesas associadas (422), inexistente/de outro usuário (404)
- [x] 37. Cenário comum — todas as rotas sem token retornam 401 sem chamar nenhum repositório

## Fechamento

- [x] 38. Rodar `dotnet test backend/GastosApp.sln` — suíte completa 100% passando (sem regressão em `Expenses`)
- [x] 39. Rodar `./scripts/export-openapi.sh` e commitar `backend/docs/openapi.json` atualizado
- [x] 40. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-16-crud-categorias/spec.md` e preencher a seção "Status" com o resumo da implementação (mirror do "Status" de `spec.md` de features anteriores)
