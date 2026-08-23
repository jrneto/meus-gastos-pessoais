# Tasks — FEAT-19: Conta (fundação multi-tenant)

Ordem pensada pra manter dependência antes de dependente (Domain →
Application → Infrastructure → Api → novo projeto Lambda → Terraform →
CI/CD → testes). Cada item é do tamanho de um commit.

## Domain

- [x] 1. Criar `Account` em `GastosApp.Domain/Accounts/Account.cs`
      (`Id`, `CreatedAt`, `Create()`/`Restore(id, createdAt)`), mesmo
      padrão de `Category`/`Expense`.
- [x] 2. Criar `Membership` + `enum MembershipRole` (só `Titular` por
      enquanto) em `GastosApp.Domain/Accounts/Membership.cs`
      (`AccountId`, `UserId`, `Role`, `CreatedAt`,
      `CreateTitular(accountId, userId)`/`Restore(...)`).
- [x] 3. Renomear `Category.UserId` → `Category.AccountId` em
      `GastosApp.Domain/Categories/Category.cs` (`Create`/`Restore`
      atualizados).
- [x] 4. Renomear `Expense.UserId` → `Expense.AccountId` em
      `GastosApp.Domain/Expenses/Expense.cs` (`Create`/`Restore`
      atualizados).

## Application

- [x] 5. Criar `IAccountRepository` + `CreateAccountResult` em
      `GastosApp.Application/Common/Interfaces/IAccountRepository.cs`.
- [x] 6. Criar `AccountErrors` (`NotResolved`, 401
      `account-not-found`) em
      `GastosApp.Application/Accounts/AccountErrors.cs`.
- [x] 7. Criar `EnsureAccountCommand` + `EnsureAccountResult` +
      `EnsureAccountCommandHandler` em
      `GastosApp.Application/Accounts/Commands/EnsureAccount/`
      (resolve via `FindAccountIdByUserIdAsync`; se não existir, chama
      `CreateAsync`).
- [x] 8. Criar `ResolveAccountIdQuery` + `ResolveAccountIdQueryHandler`
      em `GastosApp.Application/Accounts/Queries/ResolveAccountId/`
      (só lê — `null` vira `Result.Failure(AccountErrors.NotResolved)`).
- [x] 9. Renomear `userId` → `accountId` em `ICategoryRepository` e
      `IExpenseRepository`
      (`GastosApp.Application/Common/Interfaces/`).
- [x] 10. Renomear `UserId` → `AccountId` em todos os
      Commands/Queries de `Categories/` (`CreateCategoryCommand`,
      `UpdateCategoryCommand`, `DeleteCategoryCommand`,
      `GetCategoriesQuery`, `GetCategoryByIdQuery`) e seus Handlers
      (chamadas a `Category.Create`/`Category.Restore` ajustadas).
- [x] 11. Renomear `UserId` → `AccountId` em todos os
      Commands/Queries de `Expenses/` (`RegisterExpenseCommand`,
      `UpdateExpenseCommand`, `DeleteExpenseCommand`,
      `GetExpensesQuery`, `GetExpenseByIdQuery`, `ExpenseQueryFilter`)
      e seus Handlers.
- [x] 12. Atualizar `LoginUserCommandHandler`
      (`GastosApp.Application/Auth/Commands/Login/`): injetar
      `ISender` + `ILogger<LoginUserCommandHandler>`; após login
      bem-sucedido, despachar `EnsureAccountCommand` dentro de
      `try/catch` que só loga (nunca propaga — decisão técnica 2 do
      `plan.md`).

## Infrastructure

- [x] 13. Criar `DynamoDbAccountRepository`
      (`GastosApp.Infrastructure/Accounts/`): `FindAccountIdByUserIdAsync`
      (`GetItem` em `AccountPointer`) e `CreateAsync`
      (`TransactWriteItems` com os 3 itens — `AccountPointer`,
      `Account`, `Membership` — e recuperação via `GetItem` quando o
      `ConditionalCheckFailed` for no `AccountPointer`, conforme
      seção 2 do `plan.md`).
- [x] 14. Atualizar `DynamoDbCategoryRepository`: `PK = USER#{userId}`
      → `PK = ACCOUNT#{accountId}` em todos os métodos, parâmetros
      renomeados.
- [x] 15. Atualizar `DynamoDbExpenseRepository`: `PK`/`GSI1PK`
      (`USER#{userId}#{categoryId}` → `ACCOUNT#{accountId}#{categoryId}`)
      em todos os métodos, parâmetros renomeados. `GSI2PK` inalterado.
- [x] 16. Registrar `IAccountRepository → DynamoDbAccountRepository`
      em `InfrastructureServiceCollectionExtensions.cs`.

## Api

- [x] 17. Criar `CurrentAccountContext`
      (`GastosApp.Api/Common/CurrentAccountContext.cs`) — classe
      simples com `string? AccountId`.
- [x] 18. Criar `ResolveAccountEndpointFilter`
      (`GastosApp.Api/Common/`): `IEndpointFilter` que extrai `userId`
      do JWT, despacha `ResolveAccountIdQuery`, curto-circuita com
      `ResultHttpExtensions.ToHttpResult` em caso de falha (401), ou
      grava `AccountId` em `CurrentAccountContext` e segue (`next`).
- [x] 19. Registrar `CurrentAccountContext` como `Scoped` em
      `Program.cs`, e aplicar `.AddEndpointFilter<ResolveAccountEndpointFilter>()`
      nos grupos `/categories` e `/expenses` (depois de
      `.RequireAuthorization()`).
- [x] 20. Atualizar `CategoryEndpoints.cs`: as 5 extrações
      `var userId = user.FindFirst(...)` viram
      `var accountId = currentAccount.AccountId!` (injetando
      `CurrentAccountContext`).
- [x] 21. Atualizar `ExpenseEndpoints.cs`: idem, as 5 extrações.

## Novo projeto — GastosApp.CognitoTriggers

- [x] 22. Criar o projeto `GastosApp.CognitoTriggers`
      (`net10.0`, `PublishAot=true`, `InvariantGlobalization=true`,
      pacotes `Amazon.Lambda.Core`/`Amazon.Lambda.RuntimeSupport`/
      `Amazon.Lambda.Serialization.SystemTextJson`, referenciando
      `GastosApp.Application` + `GastosApp.Infrastructure`) e
      adicioná-lo à `GastosApp.sln`.
- [x] 23. Criar `CognitoPostConfirmationEvent`/
      `CognitoPostConfirmationRequest` (POCO próprio, sem pacote
      oficial da AWS — ver `plan.md` seção 1) + `JsonSerializerContext`
      source-generated pra Native AOT.
- [x] 24. Criar `AccountTriggerHandler.HandleAsync(...)` — lógica do
      handler extraída pra uma classe testável (recebe o evento +
      `ISender`/`ILogger` já resolvidos, extrai `sub` de
      `Request.UserAttributes`, despacha `EnsureAccountCommand` em
      `try/catch` que só loga, sempre retorna o evento). `Function.cs`
      fica só com o bootstrap (DI + `LambdaBootstrapBuilder`),
      delegando pra essa classe — necessário pra dar cobertura de
      teste sem subir o runtime do Lambda.
- [x] 25. Criar `Function.cs`: monta `ServiceCollection`
      (`AddApplicationServices` + `AddInfrastructure` com
      `IConfiguration` só de variável de ambiente, sem
      `AddAwsParameterStore`), resolve escopo por invocação, chama
      `AccountTriggerHandler.HandleAsync`, sobe via
      `LambdaBootstrapBuilder`.

## Infraestrutura (Terraform) — hom e prod

- [x] 26. Criar script de build do artefato do trigger
      (`infra/lambda/Dockerfile.build-account-trigger` +
      `build-account-trigger.sh`, mesmo padrão AOT/Amazon Linux 2023
      já usado pra API, publicando
      `src/GastosApp.CognitoTriggers/GastosApp.CognitoTriggers.csproj`).
- [x] 27. Criar `environments/hom/lambda-account-trigger.tf`: IAM Role
      `jrnexpenses-account-trigger-lambda-exec-hom` (só
      `dynamodb:PutItem`/`GetItem`/`TransactWriteItems` na tabela
      `GastosApp-Hom` + `logs:CreateLogStream`/`PutLogEvents`, sem
      `cognito-idp:*` nem `ssm:GetParametersByPath`), CloudWatch Log
      Group, `aws_lambda_function.account_trigger` (env var
      `DynamoDb__TableName`).
- [x] 28. Repetir a task 27 em `environments/prod/lambda-account-trigger.tf`
      (`jrnexpenses-account-trigger-lambda-exec`, tabela `GastosApp`).
- [x] 29. Adicionar `aws_lambda_permission` (hom e prod) liberando
      `lambda:InvokeFunction` pro principal `cognito-idp.amazonaws.com`,
      `source_arn = aws_cognito_user_pool.main.arn`.
- [x] 30. Adicionar `lambda_config { post_confirmation = ... }` em
      `aws_cognito_user_pool.main` (`cognito.tf`, hom e prod).
- [x] 31. Ampliar a política da IAM Role `gastosapp-backend-cicd`
      (`infra/terraform/cicd/`) para
      `lambda:UpdateFunctionCode`/`UpdateFunctionConfiguration`
      também nos dois `jrnexpenses-account-trigger{-hom}`.

## CI/CD

- [x] 32. Criar `backend-deploy-account-trigger-hom.yml`: path-filtrado
      em `backend/src/GastosApp.CognitoTriggers/**` +
      `backend/src/GastosApp.Application/**` +
      `backend/src/GastosApp.Domain/**` +
      `backend/src/GastosApp.Infrastructure/**` +
      `backend/infra/lambda/Dockerfile.build-account-trigger`/
      `build-account-trigger.sh` + `backend/GastosApp.sln`; gate de
      qualidade + build do artefato + `aws lambda update-function-code`
      + variáveis de versão (mesmo padrão de `backend-deploy-hom.yml`).
- [x] 33. Criar `backend-deploy-account-trigger-prod.yml`: disparado
      por Release `backend-v*` (mesmo gatilho de
      `backend-deploy-prod.yml`), path-filtrado calculando mudança
      desde a última release publicada (não desde o último push — ver
      decisão técnica combinada em conversa: prod também é
      path-filtrado, ao contrário do "sempre os dois juntos").

## Testes

- [x] 34. `UnitTests/Domain/AccountTests.cs` +
      `MembershipTests.cs`: `Create`/`Restore`, papel `Titular`
      default.
- [x] 35. `UnitTests/Application/EnsureAccountCommandHandlerTests.cs`:
      cria quando não existe; retorna existente sem duplicar quando já
      existe; propaga `AlreadyExisted=true` quando
      `IAccountRepository.CreateAsync` sinaliza conflito resolvido.
- [x] 36. `UnitTests/Application/ResolveAccountIdQueryHandlerTests.cs`:
      sucesso quando existe; `AccountErrors.NotResolved` (401) quando
      não existe; nunca chama `CreateAsync`.
- [x] 37. Atualizar `UnitTests/Application/LoginUserCommandHandlerTests.cs`:
      login bem-sucedido despacha `EnsureAccountCommand`; credenciais
      inválidas não despacha; exceção do `EnsureAccountCommand` é
      capturada e login segue retornando sucesso.
- [x] 38. `UnitTests/Infrastructure/DynamoDbAccountRepositoryTests.cs`:
      `FindAccountIdByUserIdAsync` (achou/não achou),
      `CreateAsync` (sucesso), `CreateAsync` sob
      `ConditionalCheckFailed` no `AccountPointer` (recupera o
      `AccountId` do vencedor via `GetItem`, `AlreadyExisted=true`).
- [x] 39. Atualizar `UnitTests/Infrastructure/DynamoDbCategoryRepositoryTests.cs`
      e os `DynamoDbExpenseRepository*Tests.cs` pra refletir
      `PK=ACCOUNT#{accountId}`/`GSI1PK` novos.
- [x] 40. Adicionar `AccountRepositoryMock` (+ `ResetAccountRepositoryMock`)
      em `ComponentTestWebApplicationFactory.cs`, mesmo padrão de
      `CategoryRepositoryMock`/`ExpenseRepositoryMock`.
- [x] 41. Atualizar `ComponentTests/Categories/CategoryEndpointsTests.cs`
      e `ComponentTests/Expenses/ExpenseEndpointsTests.cs`: configurar
      `AccountRepositoryMock` pra resolver um `accountId` de teste em
      todo cenário autenticado, e adicionar um teste de 401
      (`account-not-found`) quando a resolução falha.
- [x] 42. Adicionar em `ComponentTests/Auth/AuthEndpointsTests.cs`:
      login de usuário sem conta cria via `EnsureAccountCommand`
      (verifica chamada ao `AccountRepositoryMock`); login de usuário
      com conta existente não duplica; login com credenciais inválidas
      não chama `AccountRepositoryMock.CreateAsync`.
- [x] 43. Adicionar `GastosApp.CognitoTriggers` como
      `ProjectReference` em `GastosApp.UnitTests.csproj`.
- [x] 44. `UnitTests/CognitoTriggers/AccountTriggerHandlerTests.cs`:
      evento com `sub` válido despacha `EnsureAccountCommand` e
      retorna o evento; exceção do `EnsureAccountCommand` é capturada,
      logada, e o evento ainda é retornado (nunca propaga pro
      chamador); evento sem `sub` não despacha nada e retorna o
      evento.

## Fechamento

- [x] 45. Rodar `./scripts/export-openapi.sh` e confirmar que
      `backend/docs/openapi.json` não sofre diff de contrato (só
      possível diff incidental de metadados, sem novo endpoint/campo)
      — critério de aceite explícito da spec.
- [x] 46. Rodar a suíte completa (`dotnet test GastosApp.sln`) e
      confirmar 100% dos testes passando (`[[feedback_tests_must_pass]]`).
- [x] 47. Atualizar `spec.md`: marcar os critérios de aceite
      concluídos (`- [x]`) e adicionar a seção "Status" (mesmo padrão
      de `backend/specs/FEAT-16-crud-categorias/spec.md`) resumindo o
      que foi implementado.
