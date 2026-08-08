# FEAT-04: Registro de Despesa — Tasks

## Domain

- [x] 1. Criar `GastosApp.Domain/Expenses/ExpenseCategory.cs` (enum `Alimentacao, Transporte, Moradia, Saude, Educacao, Lazer, ComprasEServicos, Outros`)
- [x] 2. Criar `GastosApp.Domain/Expenses/Expense.cs` (entidade com construtor privado + factory `Expense.Create(userId, description, amountInCents, category, expenseDate)`)

## Application

- [x] 3. Criar `GastosApp.Application/Common/Interfaces/IExpenseRepository.cs` (`Task SaveAsync(Expense expense, CancellationToken cancellationToken = default)`)
- [x] 4. Criar `GastosApp.Application/Expenses/ExpenseErrors.cs` (`Validation(string message)` usando o slug `validation-error`)
- [x] 5. Criar `GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs` com `RegisterExpenseCommand`, `RegisterExpenseResult` e `RegisterExpenseCommandHandler` (validações de descrição/valor/categoria antes de chamar `Expense.Create` e `IExpenseRepository.SaveAsync`)

## Infrastructure

- [x] 6. Criar `GastosApp.Infrastructure/Configuration/DynamoDbOptions.cs` (`TableName` default `"GastosApp"`, `Region` default `"us-east-1"`)
- [x] 7. Registrar `IAmazonDynamoDB` e `DynamoDbOptions` em `InfrastructureServiceCollectionExtensions.AddAwsInfrastructure` (substituindo o comentário existente)
- [x] 8. Criar `GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs` implementando `IExpenseRepository.SaveAsync` via `PutItemAsync`, montando `PK`/`SK`/`GSI1PK`/`GSI1SK` conforme `data-model.md`
- [x] 9. Registrar `IExpenseRepository → DynamoDbExpenseRepository` em `InfrastructureServiceCollectionExtensions.AddInfrastructure`

## Api

- [x] 10. Criar `GastosApp.Api/Endpoints/ExpenseEndpoints.cs` com `MapExpenseEndpoints` (`POST /expenses`, grupo com `RequireAuthorization()`), extraindo `userId` da claim `sub` e mapeando `RegisterExpenseResult` via `ResultHttpExtensions.ToHttpResult` para `Results.Created($"/expenses/{id}", value)`
- [x] 11. Criar `RegisterExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate)` (DTO de request)
- [x] 12. Registrar `app.MapExpenseEndpoints();` em `Program.cs`, após `app.MapAuthEndpoints();`

## Testes unitários (`GastosApp.UnitTests`)

- [x] 13. Testes de `RegisterExpenseCommandHandler`: sucesso (chama `SaveAsync` com `Expense` correto e retorna `Result.Success`); descrição vazia/ausente/> 200 chars retorna `Result.Failure` de validação sem chamar `SaveAsync`; valor <= 0 retorna `Result.Failure` de validação sem chamar `SaveAsync`; categoria inválida retorna `Result.Failure` de validação sem chamar `SaveAsync`
- [x] 14. Teste de `Expense.Create`: gera `Id` não vazio e `CreatedAt` preenchido a partir dos dados informados

## Testes de componente (`GastosApp.ComponentTests`)

- [x] 15. Adicionar `IExpenseRepository` mock (`ExpenseRepositoryMock` + `ResetExpenseRepositoryMock()`) em `ComponentTestWebApplicationFactory`, registrado via `ConfigureTestServices`
- [x] 16. Criar `Expenses/ExpenseEndpointsTests.cs`: `POST /expenses` com dados válidos e usuário autenticado retorna 201 com `Location` e corpo esperado
- [x] 17. Teste: `POST /expenses` sem header de autenticação retorna 401 e `ExpenseRepositoryMock.SaveAsync` não é chamado
- [x] 18. Teste: `POST /expenses` com descrição vazia/ausente retorna 400 (`type` = `.../validation-error`) sem chamar `SaveAsync`
- [x] 19. Teste: `POST /expenses` com valor <= 0 retorna 400 (`type` = `.../validation-error`) sem chamar `SaveAsync`
- [x] 20. Teste: `POST /expenses` com categoria fora do enum retorna 400 (`type` = `.../validation-error`) sem chamar `SaveAsync`
- [x] 21. Teste: `POST /expenses` com data retroativa e com data futura são ambas aceitas (201)
- [x] 22. Teste: dois usuários diferentes (dois headers `TestAuthHandler` distintos) geram despesas com `UserId` correspondente ao respectivo token
- [x] 23. Teste de smoke: `ExpenseRepositoryMock.SaveAsync` lançando exceção não tratada retorna 500 (`type` = `.../internal-server-error`)

## Fechamento

- [x] 24. Rodar `dotnet test` na solução e garantir que toda a suíte (UnitTests + ComponentTests + IntegrationTests) passa
- [x] 25. Atualizar `backend/specs/FEAT-04-registro-despesa/spec.md`: marcar os itens da seção "Critérios de aceite" como concluídos (`- [x]`) e adicionar seção "Status" resumindo a implementação (seguindo o padrão de `FEAT-02`/`FEAT-03`)