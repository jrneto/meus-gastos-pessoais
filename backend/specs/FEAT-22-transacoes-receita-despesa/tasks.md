# Tasks — FEAT-22: Transações: generalizar Despesa para Receita/Despesa

Ordem pensada pra manter dependência antes de dependente (Domain →
Application → Infrastructure → Api → testes). A maior parte das tarefas
é um rename em cascata de `Expense` para `Transaction` (ver mapa
completo em `plan.md`, seção "Renomeação") — "criar" abaixo normalmente
significa "criar o arquivo novo com o conteúdo migrado e apagar o
arquivo `*Expense*` equivalente" na mesma tarefa, não uma criação do
zero. Pré-requisito de todo o build/testes locais: a tabela `GastosApp`
local (LocalStack) recriada do zero conforme `plan.md`/"Recursos AWS"
(fora deste checklist — decisão/execução do usuário).

## Domain

- [x] 1. Criar `Transaction` (`backend/src/GastosApp.Domain/Transactions/Transaction.cs`):
      `Id`/`AccountId`/`Description`/`AmountInCents`/`CategoryId`/`CreatedAt`
      migrados de `Expense`; `ExpenseDate` renomeado para `Date`; novos
      campos `Tipo` (`string`) e `CreatedByUserId` (`string`,
      obrigatório em `Create` e `Restore`, sem `?`). Apagar
      `backend/src/GastosApp.Domain/Expenses/Expense.cs` e a pasta
      `Expenses/`.

## Application — contrato de repositório e tipos auxiliares

- [x] 2. Criar `ITransactionRepository`
      (`backend/src/GastosApp.Application/Common/Interfaces/ITransactionRepository.cs`):
      mesmos métodos de `IExpenseRepository` (`SaveAsync`/`QueryAsync`/
      `DeleteAsync`/`GetByIdAsync`/`ExistsByCategoryAsync`/`UpdateAsync`),
      `UpdateAsync` ganha o parâmetro `string tipo` (sem
      `createdByUserId` — autor nunca muda). Apagar `IExpenseRepository.cs`.
- [x] 3. Criar `TransactionQueryFilter`/`TransactionQueryItem`/`TransactionQueryPage`
      (`backend/src/GastosApp.Application/Common/Interfaces/`, um arquivo
      cada, mirror dos `ExpenseQuery*` equivalentes): `TransactionQueryFilter`
      ganha `string? Tipo`; `TransactionQueryItem` ganha `string Tipo` e
      `string CreatedByUserId`, `ExpenseDate` renomeado para `Date`.
      Apagar `ExpenseQueryFilter.cs`/`ExpenseQueryItem.cs`/`ExpenseQueryPage.cs`.
- [x] 4. Criar `TransactionCursorCodec`/`TransactionCursorPayload`/`TransactionCursorJsonContext`
      (`backend/src/GastosApp.Application/Common/Cursors/`, mirror exato
      dos `ExpenseCursor*` equivalentes, sem mudança de lógica — só
      rename). Apagar os três arquivos `ExpenseCursor*.cs`.

## Application — Commands/Queries de Transaction

- [x] 5. Criar `RegisterTransactionCommand`+`RegisterTransactionCommandHandler`+`RegisterTransactionResult`
      (`backend/src/GastosApp.Application/Transactions/Commands/RegisterTransaction/RegisterTransactionCommand.cs`):
      Command ganha `Tipo`/`Date`(renomeado)/`CreatedByUserId`; Handler
      chama `Transaction.Create(...)` com esses campos e retorna
      `RegisterTransactionResult.FromEntity(transaction, createdByLabel: "Você")`
      (autor de um `POST` é sempre o próprio chamador — sem consultar
      `IMembershipRepository`); `RegisterTransactionResult` ganha
      `Tipo`/`Date`/`CreatedByUserId`/`CreatedByLabel`. Apagar
      `backend/src/GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs`
      e a pasta `Expenses/`.
- [x] 6. Criar `RegisterTransactionCommandValidator`
      (`.../RegisterTransaction/RegisterTransactionCommandValidator.cs`):
      regras de `Description`/`AmountInCents` mantidas de
      `RegisterExpenseCommandValidator`; nova regra de `Tipo`
      (`NotEmpty` + `Must(t => t is "despesa" or "receita")`); regra de
      `CategoryId` trocada de `BeAnOwnedCategoryAsync` para
      `BeAnOwnedCategoryOfMatchingTypeAsync` (busca a categoria uma vez
      e confere existência + `category.Tipo == command.Tipo` no mesmo
      predicado, mensagem única "Categoria inválida." pros três casos).
- [x] 7. Criar `CreatedByLabelResolver`
      (`backend/src/GastosApp.Application/Transactions/Common/CreatedByLabelResolver.cs`,
      helper estático novo): `"Você"` quando `createdByUserId == callerUserId`;
      senão consulta `IMembershipRepository.FindByAccountAndUserIdAsync`
      e retorna `membership.Email`, ou `"Ex-membro"` se o `Membership`
      não existir mais.
- [x] 8. Criar `UpdateTransactionCommand`+`UpdateTransactionCommandHandler`+`UpdateTransactionResult`
      (`.../Transactions/Commands/UpdateTransaction/UpdateTransactionCommand.cs`):
      Command ganha `CallerUserId`/`CallerRole`/`Tipo`/`Date`; Handler
      busca a transação via `GetByIdAsync` primeiro (404 se não
      encontrar), retorna `MembershipErrors.InsufficientPermission`
      quando `CallerRole == Lancar` e `existing.CreatedByUserId != CallerUserId`,
      senão chama `UpdateAsync(...)` e resolve `createdByLabel` via
      `CreatedByLabelResolver` antes de montar o `Result`. Apagar
      `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommand.cs`.
- [x] 9. Criar `UpdateTransactionCommandValidator`
      (`.../UpdateTransaction/UpdateTransactionCommandValidator.cs`,
      mesmas regras da task 6). Apagar
      `.../Expenses/Commands/UpdateExpense/UpdateExpenseCommandValidator.cs`.
- [x] 10. Criar `DeleteTransactionCommand`+`DeleteTransactionCommandHandler`
      (`.../Transactions/Commands/DeleteTransaction/DeleteTransactionCommand.cs`):
      mesmo formato de posse da task 8 (`GetByIdAsync` → checagem de
      papel/autoria → `DeleteAsync`), sem `Result<T>` (só `Result`).
      Apagar `backend/src/GastosApp.Application/Expenses/Commands/DeleteExpense/DeleteExpenseCommand.cs`.
- [x] 11. Criar `GetTransactionsQuery`+`GetTransactionsQueryHandler`+`GetTransactionsResult`+`TransactionSummary`
      (`.../Transactions/Queries/GetTransactions/GetTransactionsQuery.cs`):
      Query ganha `CallerUserId`/`Tipo`; Handler monta o
      `TransactionQueryFilter` com `Tipo`, itera a página resolvendo
      `createdByLabel` por item via `CreatedByLabelResolver` com um
      `Dictionary<string, string>` de cache por `createdByUserId`
      dentro da própria página (evita repetir
      `FindByAccountAndUserIdAsync` pro mesmo autor). Apagar
      `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs`
      e a pasta `Expenses/`.
- [x] 12. Criar `GetTransactionsQueryValidator`
      (`.../GetTransactions/GetTransactionsQueryValidator.cs`, mirror de
      `GetExpensesQueryValidator` — `yearMonth`/`dateFrom`/`dateTo`/
      `minAmountInCents`/`maxAmountInCents`/`limit`/`cursor` inalterados,
      usando `TransactionCursorCodec`): nova regra `Tipo` (`null`,
      `"despesa"` ou `"receita"`, senão inválido). Apagar
      `.../Expenses/Queries/GetExpenses/GetExpensesQueryValidator.cs`.
- [x] 13. Criar `GetTransactionByIdQuery`+`GetTransactionByIdQueryHandler`
      (`.../Transactions/Queries/GetTransactionById/GetTransactionByIdQuery.cs`):
      Query ganha `CallerUserId`; Handler busca a transação, resolve
      `createdByLabel` via `CreatedByLabelResolver`, retorna o mesmo
      shape de `UpdateTransactionResult` (mesmo padrão que
      `GetExpenseByIdQuery` já usava com `UpdateExpenseResult`). Apagar
      `.../Expenses/Queries/GetExpenseById/GetExpenseByIdQuery.cs`.
- [x] 14. Criar `TransactionErrors`
      (`backend/src/GastosApp.Application/Transactions/TransactionErrors.cs`,
      só `NotFound` — sem `Error` de permissão próprio, reaproveita
      `MembershipErrors.InsufficientPermission`). Apagar
      `backend/src/GastosApp.Application/Expenses/ExpenseErrors.cs`.

## Application — ajustes em Category e DI

- [x] 15. Atualizar `DeleteCategoryCommandHandler`
      (`backend/src/GastosApp.Application/Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs`):
      trocar a dependência `IExpenseRepository` por
      `ITransactionRepository` (mesmo método `ExistsByCategoryAsync`,
      só o tipo do campo/parâmetro muda).
- [x] 16. Atualizar `CategoryErrors.CategoryInUse`
      (`backend/src/GastosApp.Application/Categories/CategoryErrors.cs`):
      só o texto da mensagem, de "despesas" pra "transações" — código
      `category-in-use` e `ErrorType.UnprocessableEntity` inalterados.
- [x] 17. Atualizar `ApplicationServiceCollectionExtensions.AddApplicationServices`
      (`backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`):
      trocar os três registros `IValidator<RegisterExpenseCommand>`/
      `IValidator<UpdateExpenseCommand>`/`IValidator<GetExpensesQuery>`
      pelos equivalentes de `Transaction`.

## Infrastructure

- [x] 18. Criar `DynamoDbTransactionRepository` — parte 1: `SaveAsync`/
      `GetByIdAsync`/`UpdateAsync`/`DeleteAsync`/`ExistsByCategoryAsync`
      (`backend/src/GastosApp.Infrastructure/Transactions/DynamoDbTransactionRepository.cs`):
      `SaveAsync` grava `Tipo` com `transaction.Tipo` (não mais a
      constante `"despesa"`), `CreatedByUserId` sempre, atributo de
      data renomeado pra `Date`; `IsDespesaItem` generalizado pra
      `IsTransactionItem` (`tipo.S != "categoria"`, aceita os dois
      valores); `DeleteAsync` com `ConditionExpression` generalizada
      (`#tipo <> :tipoCategoria`, sem enumerar `"despesa"`/`"receita"`);
      `GetByIdAsync`/`UpdateAsync` leem `CreatedByUserId`/`Date`
      diretamente (sem `TryGetValue` defensivo — tabela sem item
      legado), `UpdateAsync` preserva `CreatedByUserId` do item atual
      no item novo.
- [x] 19. Completar `DynamoDbTransactionRepository` — parte 2:
      `QueryAsync`/`BuildQueryRequest`/`MapToTransactionQueryItem`: novo
      método `BuildFilterExpression` (renomeado de
      `BuildAmountFilterExpression`) combina o filtro de `Tipo`
      (`FilterExpression` do próprio DynamoDB, sem ressalva de
      "ausente = default") com o filtro de valor já existente;
      `MapToTransactionQueryItem` lê `Tipo`/`CreatedByUserId`/`Date`
      (atributo `Date`, não mais `ExpenseDate`). Apagar
      `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs`
      e a pasta `Expenses/`.
- [x] 20. Atualizar `InfrastructureServiceCollectionExtensions`
      (`backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`):
      trocar `services.AddScoped<IExpenseRepository, DynamoDbExpenseRepository>();`
      por `services.AddScoped<ITransactionRepository, DynamoDbTransactionRepository>();`.

## Api

- [x] 21. Atualizar `CurrentAccountContext`
      (`backend/src/GastosApp.Api/Common/CurrentAccountContext.cs`):
      novo campo `public string? UserId { get; set; }`.
- [x] 22. Atualizar `ResolveAccountEndpointFilter`
      (`backend/src/GastosApp.Api/Common/ResolveAccountEndpointFilter.cs`):
      popular `_currentAccount.UserId = userId;` junto com
      `AccountId`/`MembershipId`/`Role` (mesma variável `userId` já
      extraída da claim `sub` no início do método).
- [x] 23. Criar `TransactionEndpoints`
      (`backend/src/GastosApp.Api/Endpoints/TransactionEndpoints.cs`,
      grupo `/transactions`): `RegisterTransaction`/`GetTransactions`/
      `GetTransactionById`/`UpdateTransaction`/`DeleteTransaction` +
      `RegisterTransactionRequest`/`UpdateTransactionRequest`/
      `GetTransactionsRequest` (com `Tipo`); `PUT`/`DELETE` passam a
      permitir `MembershipRole.Lancar` no
      `RoleEndpointFilters.Require(...)` (posse checada no Handler,
      task 8/10); handlers passam `currentAccount.UserId!` (`POST`) e
      `currentAccount.UserId!`/`currentAccount.Role!.Value` (`PUT`/
      `DELETE`) pros Commands, e `currentAccount.UserId!` (`GET`) pra
      `GetTransactionsQuery`/`GetTransactionByIdQuery`. Apagar
      `backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs`.
- [x] 24. Atualizar `Program.cs`
      (`backend/src/GastosApp.Api/Program.cs`):
      `app.MapExpenseEndpoints();` → `app.MapTransactionEndpoints();`.
- [x] 25. Atualizar `AppJsonSerializerContext.cs`
      (`backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`):
      trocar as 7 entradas `Expense*`/`GetExpenses*` pelas equivalentes
      `Transaction*`/`GetTransactions*`.
- [x] 26. Rodar `dotnet build backend/GastosApp.sln` e corrigir todos os
      erros de compilação (Domain/Application/Infrastructure/Api) antes
      de seguir para os testes.

## Testes unitários (`backend/tests/GastosApp.UnitTests/`)

- [x] 27. Criar `Domain/TransactionTests.cs` (a partir de
      `ExpenseTests.cs`): `Create`/`Restore` cobrindo `tipo`/
      `createdByUserId`/`date`. Apagar `Domain/ExpenseTests.cs`.
- [x] 28. Criar `Application/RegisterTransactionCommandValidatorTests.cs`
      (a partir de `RegisterExpenseCommandValidatorTests.cs`): + casos
      de `tipo` ausente/vazio/fora de `despesa`\|`receita` → inválido;
      `categoryId` de categoria com `tipo` divergente → inválido; `tipo`
      batendo com a categoria → válido. Apagar
      `Application/RegisterExpenseCommandValidatorTests.cs`.
- [x] 29. Criar `Application/UpdateTransactionCommandValidatorTests.cs`
      (mesmos casos da task 28, mirror). Apagar
      `Application/UpdateExpenseCommandValidatorTests.cs`.
- [x] 30. Criar `Application/RegisterTransactionCommandHandlerTests.cs`
      (a partir de `RegisterExpenseCommandHandlerTests.cs`): `Result`
      sempre com `createdByUserId` igual ao informado no Command e
      `createdByLabel == "Você"`. Apagar
      `Application/RegisterExpenseCommandHandlerTests.cs`.
- [x] 31. Criar `Application/UpdateTransactionCommandHandlerTests.cs`
      (a partir de `UpdateExpenseCommandHandlerTests.cs`): + casos —
      `CallerRole=Lancar` + `CreatedByUserId` igual ao chamador →
      sucesso; `CallerRole=Lancar` + `CreatedByUserId` diferente →
      `Result.Failure(MembershipErrors.InsufficientPermission)`,
      `ITransactionRepository.UpdateAsync` mockado nunca chamado;
      `CallerRole=Total`/`Titular` numa transação de outro autor →
      sucesso; `GetByIdAsync` mockado retornando `null` → `NotFound`
      sem chamar `UpdateAsync`. Apagar
      `Application/UpdateExpenseCommandHandlerTests.cs`.
- [x] 32. Criar `Application/DeleteTransactionCommandHandlerTests.cs`
      (a partir de `DeleteExpenseCommandHandlerTests.cs`): mesmos 4
      casos de posse da task 31, espelhados pra exclusão. Apagar
      `Application/DeleteExpenseCommandHandlerTests.cs`.
- [x] 33. Criar `Application/GetTransactionByIdQueryHandlerTests.cs`
      (a partir de `GetExpenseByIdQueryHandlerTests.cs`):
      `createdByLabel == "Você"` quando `CreatedByUserId == CallerUserId`;
      e-mail do `Membership` mockado quando é outro autor; `"Ex-membro"`
      quando `IMembershipRepository.FindByAccountAndUserIdAsync` mockado
      retorna `null`. Apagar
      `Application/GetExpenseByIdQueryHandlerTests.cs`.
- [x] 34. Criar `Application/GetTransactionsQueryHandlerTests.cs` (a
      partir de `GetExpensesQueryHandlerTests.cs`): filtro `Tipo`
      repassado ao `ITransactionRepository.QueryAsync` mockado; + caso
      novo — página com duas transações do mesmo `CreatedByUserId`
      (outro autor) resulta em só uma chamada a
      `FindByAccountAndUserIdAsync` (comprova o cache por página).
      Apagar `Application/GetExpensesQueryHandlerTests.cs`.
- [x] 35. Criar `Application/GetTransactionsQueryValidatorTests.cs` (a
      partir de `GetExpensesQueryValidatorTests.cs`): + caso de `tipo`
      `null`/`"despesa"`/`"receita"` → válido, qualquer outro → inválido.
      Apagar `Application/GetExpensesQueryValidatorTests.cs`.
- [x] 36. Criar `Infrastructure/DynamoDbTransactionRepositorySaveTests.cs`
      (novo — não existe hoje um `DynamoDbExpenseRepositorySaveTests.cs`,
      `SaveAsync` só era exercitado indiretamente): `Tipo` gravado igual
      a `transaction.Tipo` (não mais constante); `CreatedByUserId`
      sempre presente no item; atributo de data gravado como `Date`.
- [x] 37. Criar `Infrastructure/DynamoDbTransactionRepositoryGetByIdTests.cs`
      (a partir de `DynamoDbExpenseRepositoryGetByIdTests.cs`): `Tipo`
      aceitando `"despesa"` e `"receita"` como item válido
      (`IsTransactionItem`); item com `Tipo="categoria"` continua
      rejeitado; `Date` lido do atributo `Date`. Apagar
      `Infrastructure/DynamoDbExpenseRepositoryGetByIdTests.cs`.
- [x] 38. Criar `Infrastructure/DynamoDbTransactionRepositoryUpdateTests.cs`
      (a partir de `DynamoDbExpenseRepositoryUpdateTests.cs`):
      `CreatedByUserId` do item atual preservado no item novo depois da
      edição. Apagar `Infrastructure/DynamoDbExpenseRepositoryUpdateTests.cs`.
- [x] 39. Criar `Infrastructure/DynamoDbTransactionRepositoryDeleteTests.cs`
      (a partir de `DynamoDbExpenseRepositoryDeleteTests.cs`):
      `ConditionExpression` nova (`#tipo <> :tipoCategoria`) — apagar
      item `Tipo="receita"` funciona; item `Tipo="categoria"` continua
      bloqueado. Apagar `Infrastructure/DynamoDbExpenseRepositoryDeleteTests.cs`.
- [x] 40. Criar `Infrastructure/DynamoDbTransactionRepositoryQueryTests.cs`
      (a partir de `DynamoDbExpenseRepositoryQueryTests.cs`):
      `FilterExpression` inclui `Tipo` quando `filter.Tipo` informado,
      combinado com filtro de valor quando os dois estão presentes.
      Apagar `Infrastructure/DynamoDbExpenseRepositoryQueryTests.cs`.
- [x] 41. Criar `Infrastructure/DynamoDbTransactionRepositoryExistsByCategoryTests.cs`
      (mirror direto de `DynamoDbExpenseRepositoryExistsByCategoryTests.cs`,
      sem mudança de lógica). Apagar
      `Infrastructure/DynamoDbExpenseRepositoryExistsByCategoryTests.cs`.

## Testes de componente (`backend/tests/GastosApp.ComponentTests/`)

- [x] 42. Criar `Transactions/TransactionEndpointsTests.cs` — parte 1
      (a partir de `Expenses/ExpenseEndpointsTests.cs`): CRUD básico e
      validação — `POST` despesa/receita válidas (201, `createdByLabel="Você"`);
      `tipo` ausente/inválido (400); `tipo` divergente da categoria
      (400); `categoryId` inexistente/de outra conta (400); `GET`
      listagem sem filtro, com `?tipo=`, com `?tipo=` inválido (400),
      combinando `tipo`+`categoryId`+`yearMonth`+datas+valor; `GET /{id}`
      inexistente/de outra conta (404); 401 sem token em todas as rotas.
- [x] 43. Completar `Transactions/TransactionEndpointsTests.cs` — parte 2:
      autorização e posse — `PUT`/`DELETE` por `Total`/`Titular` em
      transação própria e de outro membro (sucesso nos dois); `PUT`/
      `DELETE` por `Lancar` em transação própria (sucesso) e de outro
      membro (403); `PUT`/`DELETE`/`POST` por `Leitura` (403 nos três);
      `GET /{id}` de transação de outro membro retornando o e-mail dele
      em `createdByLabel`; isolamento entre contas (`PUT`/`DELETE`/`GET`
      de transação de outra conta → 404). Apagar
      `backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs`
      e a pasta `Expenses/`.
- [x] 44. Atualizar `Categories/CategoryEndpointsTests.cs`: trocar o mock
      de `IExpenseRepository` por `ITransactionRepository` nos casos já
      existentes de `DELETE /categories/{id}` bloqueado por uso (422)
      — sem cenário novo, só o tipo mockado.

## Fechamento

- [x] 45. Rodar `dotnet test backend/GastosApp.sln` — suíte completa
      100% passando, sem regressão em `Categories`/`Members`.
- [x] 46. Rodar `./scripts/export-openapi.sh` e conferir
      `backend/docs/openapi.json`: `git diff` deve mostrar a remoção
      completa de `/expenses` e o `/transactions` novo (`GET`/`POST`/
      `PUT`/`DELETE`, incluindo `/{id}`), os schemas com `tipo`/`date`/
      `createdByUserId`/`createdByLabel`, e o novo parâmetro de query
      `tipo`; commitar o arquivo atualizado.
- [x] 47. Atualizar `spec.md`: marcar todos os critérios de aceite
      concluídos (`- [x]`) e adicionar a seção "Status" (mesmo padrão
      de `backend/specs/FEAT-21-categoria-tipo-orcamento/spec.md`)
      resumindo o que foi implementado, incluindo a nota de que a
      tabela `GastosApp` foi recriada do zero antes do deploy.
