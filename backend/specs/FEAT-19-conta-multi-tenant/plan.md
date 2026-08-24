# Plan — FEAT-19: Conta (fundação multi-tenant)

Decisões de arquitetura já fechadas em conversa (registradas aqui para
não se perderem): novo projeto `GastosApp.CognitoTriggers` na mesma
`.sln`, reaproveitando `Application`/`Infrastructure` como composition
root próprio (sem violar Clean Architecture — ver justificativa na
seção "Decisões técnicas"); sem emulação do trigger em ambiente local
(local roda só pelo fallback do login); CI/CD com dois workflows de
deploy path-filtrados (hom e prod).

## 1. Camadas afetadas

### Domain — `GastosApp.Domain`
- **Novo** `Accounts/Account.cs`: entidade `Account` (`Id`, `CreatedAt`).
- **Novo** `Accounts/Membership.cs`: entidade `Membership`
  (`AccountId`, `UserId`, `Role`, `CreatedAt`) + `enum MembershipRole`
  (só `Titular` por enquanto — `Leitura`/`Lancar`/`Total` entram na
  FEAT-20).
- `Categories/Category.cs`: propriedade `UserId` → `AccountId` (mesmo
  formato, `Category.Create(accountId, ...)`).
- `Expenses/Expense.cs`: propriedade `UserId` → `AccountId`, mesmo
  padrão.

### Application — `GastosApp.Application`
- **Novo** `Common/Interfaces/IAccountRepository.cs`:
  ```csharp
  public sealed record CreateAccountResult(string AccountId, bool AlreadyExisted);

  public interface IAccountRepository
  {
      Task<string?> FindAccountIdByUserIdAsync(string userId, CancellationToken cancellationToken = default);
      Task<CreateAccountResult> CreateAsync(string userId, CancellationToken cancellationToken = default);
  }
  ```
- **Novo** `Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`:
  ```csharp
  public sealed record EnsureAccountCommand(string UserId) : ICommand<Result<EnsureAccountResult>>;
  public sealed record EnsureAccountResult(string AccountId, bool AlreadyExisted);
  ```
  Handler: `FindAccountIdByUserIdAsync` → se achou, retorna
  `Success(existing, AlreadyExisted: true)`; se não, chama
  `CreateAsync` (que já resolve a concorrência internamente — ver seção
  3) e retorna o resultado. Nunca falha por regra de negócio (sem
  `IValidator`, `UserId` já vem confiável do JWT) — só propaga exceção
  de infraestrutura genuína (ver decisão técnica 2).
- **Novo** `Accounts/Queries/ResolveAccountId/ResolveAccountIdQuery.cs`:
  ```csharp
  public sealed record ResolveAccountIdQuery(string UserId) : IQuery<Result<string>>;
  ```
  Handler: `FindAccountIdByUserIdAsync` → `null` vira
  `Result.Failure<string>(AccountErrors.NotResolved)` (401). **Nunca
  cria** — só resolve (usado pelo filtro de `Category`/`Expense`,
  nunca pelo login).
- **Novo** `Accounts/AccountErrors.cs`:
  ```csharp
  public static class AccountErrors
  {
      public static Error NotResolved =>
          Error.Unauthorized("account-not-found", "Conta não encontrada para este usuário.");
  }
  ```
- `Auth/Commands/Login/LoginUserCommand.cs`: `LoginUserCommandHandler`
  ganha `ISender` e `ILogger<LoginUserCommandHandler>` no construtor;
  após login bem-sucedido, despacha `EnsureAccountCommand` (ver decisão
  técnica 2 sobre tratamento de falha).
- `ICategoryRepository`/`IExpenseRepository`: todo parâmetro `userId`
  vira `accountId` (mesma assinatura, semântica nova).
- Todos os Commands/Queries de `Categories/`
  (`CreateCategoryCommand`, `UpdateCategoryCommand`,
  `DeleteCategoryCommand`, `GetCategoriesQuery`,
  `GetCategoryByIdQuery`) e `Expenses/`
  (`RegisterExpenseCommand`, `UpdateExpenseCommand`,
  `DeleteExpenseCommand`, `GetExpensesQuery`, `GetExpenseByIdQuery`,
  `ExpenseQueryFilter`): campo `UserId` → `AccountId` (rename mecânico,
  sem mudança de comportamento).

### Infrastructure — `GastosApp.Infrastructure`
- **Novo** `Accounts/DynamoDbAccountRepository.cs` — ver modelo de
  dados na seção 2.
- `Categories/DynamoDbCategoryRepository.cs`: `PK = $"USER#{userId}"` →
  `PK = $"ACCOUNT#{accountId}"` em todos os métodos; parâmetros
  renomeados.
- `Expenses/DynamoDbExpenseRepository.cs`: idem, incluindo `GSI1PK`
  (`USER#{userId}#{categoryId}` → `ACCOUNT#{accountId}#{categoryId}`).
  `GSI2PK` (`ID#{id}`) não muda — já é neutro quanto a tenant.
- `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`:
  registra `IAccountRepository → DynamoDbAccountRepository`.

### Api — `GastosApp.Api`
- **Novo** `Common/CurrentAccountContext.cs` — serviço `Scoped` com uma
  propriedade `string? AccountId`, preenchida pelo filtro abaixo.
- **Novo** `Common/ResolveAccountEndpointFilter.cs` — `IEndpointFilter`
  registrado via `.AddEndpointFilter<ResolveAccountEndpointFilter>()`
  nos grupos `/categories` e `/expenses` (depois de
  `.RequireAuthorization()`). Extrai `userId` do JWT (mesmo padrão já
  usado em todo endpoint hoje), despacha `ResolveAccountIdQuery` via
  `ISender`, e:
  - falha (401, `account-not-found`) → curto-circuita reaproveitando
    `ResultHttpExtensions.ToHttpResult` (mesmo formato de erro do
    resto da API, sem duplicar `ProblemDetails`)
  - sucesso → grava `AccountId` em `CurrentAccountContext` e chama
    `next(context)`
- `CategoryEndpoints.cs`/`ExpenseEndpoints.cs`: as 10 linhas
  `var userId = user.FindFirst("sub")?.Value ?? ...` viram
  `var accountId = currentAccount.AccountId!` (injeção do
  `CurrentAccountContext`, não-nulo garantido pelo filtro). `/auth/*`
  não muda — continua extraindo `userId` do JWT normalmente (não usa
  `accountId`).
- `Program.cs`: registra `CurrentAccountContext` como `Scoped`.

### Novo projeto — `GastosApp.CognitoTriggers`
- `GastosApp.CognitoTriggers.csproj`: `net10.0`, `PublishAot=true`,
  `InvariantGlobalization=true`. Pacotes: `Amazon.Lambda.Core`,
  `Amazon.Lambda.RuntimeSupport`,
  `Amazon.Lambda.Serialization.SystemTextJson`. `ProjectReference`:
  `GastosApp.Application`, `GastosApp.Infrastructure` (não referencia
  `GastosApp.Api`).
- `CognitoPostConfirmationEvent.cs`: **não existe pacote oficial da AWS
  com tipos para User Pool Lambda triggers em .NET**
  (`Amazon.Lambda.CognitoEvents` é só Cognito Sync, serviço distinto e
  hoje sem uso aqui — verificado antes de assumir, para não inventar
  contrato). POCO próprio, formato documentado pela AWS
  (`docs.aws.amazon.com/cognito/.../user-pool-lambda-post-confirmation`):
  ```csharp
  public sealed class CognitoPostConfirmationEvent
  {
      public string Version { get; set; } = "";
      public string Region { get; set; } = "";
      public string UserPoolId { get; set; } = "";
      public string UserName { get; set; } = "";
      public string TriggerSource { get; set; } = "";
      public CognitoPostConfirmationRequest Request { get; set; } = new();
      public Dictionary<string, object> Response { get; set; } = new();
  }

  public sealed class CognitoPostConfirmationRequest
  {
      public Dictionary<string, string> UserAttributes { get; set; } = new();
  }
  ```
  + `JsonSerializerContext` source-generated (mesmo padrão de
  `AppJsonSerializerContext`/`LambdaEventJsonSerializerContext` já
  usados em `GastosApp.Api`, obrigatório sob Native AOT).
- `Function.cs` (composition root deste Lambda):
  ```csharp
  // Sem AddAwsParameterStore aqui (decidido: nenhuma leitura de SSM
  // neste Lambda) — configuração só de variável de ambiente
  // (DynamoDb__TableName etc.), já suficiente pro que AddInfrastructure
  // precisa registrar (cliente DynamoDB + DynamoDbAccountRepository).
  var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

  var services = new ServiceCollection();
  services.AddApplicationServices();
  services.AddInfrastructure(configuration, environment: "Lambda"); // mesma extensão já usada pela Api
  var provider = services.BuildServiceProvider();

  var handler = async (CognitoPostConfirmationEvent evt, ILambdaContext context) =>
  {
      using var scope = provider.CreateScope();
      var sender = scope.ServiceProvider.GetRequiredService<ISender>();
      var logger = scope.ServiceProvider.GetRequiredService<ILogger<...>>();

      if (evt.Request.UserAttributes.TryGetValue("sub", out var userId) && !string.IsNullOrEmpty(userId))
      {
          try
          {
              await sender.Send(new EnsureAccountCommand(userId), CancellationToken.None); // confirmado: sem CancellationTokenSource amarrado ao timeout do Lambda
          }
          catch (Exception ex)
          {
              // nunca propaga — ver decisão técnica 2: Cognito bloqueia a
              // confirmação se o trigger lançar/retornar erro
              logger.LogError(ex, "Falha ao garantir Account para o usuário {UserId} no trigger PostConfirmation.", userId);
          }
      }

      return evt; // Cognito exige o evento de volta, alterado ou não
  };

  await LambdaBootstrapBuilder.Create(handler,
          new SourceGeneratorLambdaJsonSerializer<CognitoTriggerJsonSerializerContext>())
      .Build()
      .RunAsync();
  ```
  Roda para `PostConfirmation_ConfirmSignUp`, `PostConfirmation_ConfirmForgotPassword` e
  `PostConfirmation_AdminConfirmSignUp` (os 3 `triggerSource` documentados
  pela AWS para este trigger) — não precisa distinguir entre eles,
  `EnsureAccountCommand` é idempotente nos três casos.

## 2. Modelo de dados (DynamoDB, tabela `GastosApp` já existente)

Nenhuma tabela nem índice novo — reaproveita `GSI1` (já provisionado,
projeção `ALL`), do jeito que `backend/docs/roadmap.md` já previa
("GSI1 PK=USER#userId já modelado pra isso desde já").

### `AccountPointer` (resolução + trava de concorrência)
| Atributo | Valor |
|---|---|
| `PK` | `USER#<userId>` |
| `SK` | `ACCOUNT#` (literal fixo) |
| `AccountId` | string |

Depois desta feature, `PK=USER#<userId>` deixa de ser usado por
`Category`/`Expense` (migram para `ACCOUNT#<accountId>` — ver abaixo),
então não há colisão de espaço de chave com os itens que já existiam
ali.

### `Account` (metadado da conta)
| Atributo | Valor |
|---|---|
| `PK` | `ACCOUNT#<accountId>` |
| `SK` | `ACCOUNT#` (literal fixo) |
| `CreatedAt` | ISO 8601 |

### `Membership`
| Atributo | Valor |
|---|---|
| `PK` | `ACCOUNT#<accountId>` |
| `SK` | `MEMBER#<userId>` |
| `GSI1PK` | `USER#<userId>` |
| `GSI1SK` | `ACCOUNT#<accountId>` |
| `Role` | `"Titular"` |
| `CreatedAt` | ISO 8601 |

`GSI1PK`/`GSI1SK` não são usados por esta feature (a resolução usa
`AccountPointer`, um `GetItem` direto, mais barato que `Query`) — mas já
deixam o item pronto pro access pattern "quais contas esse usuário
pertence" que a FEAT-20 vai precisar, sem exigir migração depois.

### `Category`/`Expense` (migração)
- `PK`: `USER#<userId>` → `ACCOUNT#<accountId>`
- `Expense.GSI1PK`: `USER#<userId>#<categoryId>` →
  `ACCOUNT#<accountId>#<categoryId>`
- Resto inalterado (`SK`, `GSI2PK=ID#<id>`, atributos)

### Resolução (`FindAccountIdByUserIdAsync`)
`GetItem(PK=USER#<userId>, SK=ACCOUNT#)` → `AccountId` ou `null`.
Usado tanto por `EnsureAccountCommand` (decide se cria) quanto por
`ResolveAccountIdQuery` (só lê, nunca cria).

### Criação (`CreateAsync`, dentro de `DynamoDbAccountRepository`)
`TransactWriteItems` com 3 `Put`:
1. `AccountPointer` (`PK=USER#<userId>, SK=ACCOUNT#`) —
   `ConditionExpression: attribute_not_exists(PK)`. **Este é o único
   item cuja condição realmente serializa a concorrência** — é o único
   com chave determinística a partir só do `userId`, calculável antes
   de gerar o `accountId`.
2. `Account` (`PK=ACCOUNT#<novoGuid>, SK=ACCOUNT#`) — condição também
   presente por defesa, mas nunca é ela quem barra a corrida (a chave
   usa um GUID novo, praticamente impossível colidir).
3. `Membership` (`PK=ACCOUNT#<novoGuid>, SK=MEMBER#<userId>`, `Role=Titular`).

Se a transação falhar com `TransactionCanceledException` e o motivo for
`ConditionalCheckFailed` no item 1 (alguém já criou a conta desse
usuário entre o `FindAccountIdByUserIdAsync` e este `Put` — a corrida
que a US7 da spec cobre): `GetItem` no mesmo `AccountPointer` pra
recuperar o `AccountId` do vencedor, e retorna
`CreateAccountResult(vencedor, AlreadyExisted: true)`. Mesmo padrão já
usado em `DynamoDbCategoryRepository.UpdateAsync` (`TransactWriteItems`
+ interpretar `CancellationReasons` pra decidir entre `NotFound` e
`NameConflict`).

## 3. Decisões técnicas

**1. `GastosApp.CognitoTriggers` referencia `Application` +
`Infrastructure` diretamente (não um projeto de composition root à
parte).** Mesma relação que `GastosApp.Api` já tem hoje (confirmado nos
`.csproj`: `Api → Application, Infrastructure`). Não fere Clean
Architecture — `Domain`/`Application` continuam sem saber que
DynamoDB/Lambda existem; é o entry point (agora dois: `Api` e
`CognitoTriggers`) que monta o grafo de DI, papel de composition root.
Ver discussão completa no histórico da conversa desta feature.

**2. O trigger e o fallback do login nunca propagam falha de
`EnsureAccountCommand` pro chamador.** Confirmado na documentação da
AWS: o Post Confirmation trigger é invocado de forma síncrona como
parte da própria chamada `ConfirmSignUp`/`AdminConfirmSignUp`
/`ConfirmForgotPassword` — se o Lambda lançar erro, a confirmação
falha pro usuário. Como a spec exige "falha transitória do trigger
nunca impede a confirmação" (US, critério de aceite), `Function.cs`
**sempre** captura qualquer exceção do `EnsureAccountCommand`, loga, e
devolve o evento normalmente pro Cognito. Pelo mesmo motivo,
`LoginUserCommandHandler` também captura (não deixa a exceção subir
pro `GlobalExceptionHandler`, que devolveria 500 pro login inteiro).
Em ambos os casos isso é resiliência de efeito colateral, não fluxo de
negócio — não conflita com a regra da constitution de "proibido lançar
exceção para fluxo de negócio" (aqui é o oposto: é pra proteger o fluxo
principal de uma falha em efeito colateral).

**3. `EnsureAccountCommand` é despachado via `ISender` a partir de
`LoginUserCommandHandler` (Mediator chamando Mediator).** Alternativa
considerada: extrair um serviço simples (`IAccountProvisioningService`)
sem passar pelo Mediator, evitando dispatch aninhado. Optei por manter
via `EnsureAccountCommand`/`ISender` por consistência — é o único
padrão de reuso de caso de uso já existente no projeto, os dois
consumidores (`Function.cs` do trigger e `LoginUserCommandHandler`) já
precisam de `ISender` de qualquer forma, e o comando não tem
`IValidator` (sem risco de pipeline complexo) nem propriedades que
convidem a ciclos.

**4. Filtro de resolução de conta usa `ResolveAccountIdQuery`, nunca
`EnsureAccountCommand`.** Categoria/despesa só *leem* a conta já
resolvida — nunca criam (US da spec: ausência de conta em
`Category`/`Expense` é 401, não auto-cura). Só o login tem o
comportamento de criar.

**5. `local-init.sh`/`docker-compose.yml` não mudam nesta feature**
(decisão já fechada em conversa) — `cognito-local` não tem
`PostConfirmation` configurado em `TriggerFunctions`, então a
confirmação local segue sem disparar nada, e a conta nasce pelo
fallback do login. `GastosApp.CognitoTriggers` é testado isolado via
ComponentTest, invocando o handler diretamente com um evento construído
em memória e `IAccountRepository` mockado.

## 4. Recursos AWS usados ou afetados

**Recursos novos** (aprovados explicitamente pelo usuário nesta
feature):
- 1 função Lambda por ambiente (`hom`, `prod`):
  `jrnexpenses-account-trigger-{hom|}`, runtime `provided.al2023`,
  artefato próprio (`infra/lambda/account-trigger-function.zip`, build
  separado — novo `Dockerfile.build-account-trigger`/
  `build-account-trigger.sh`, mesmo padrão do artefato da API)
- 1 IAM Role de execução por ambiente
  (`jrnexpenses-account-trigger-lambda-exec-{hom|}`), permissões restritas
  a `dynamodb:PutItem`/`GetItem`/`TransactWriteItems` na tabela
  `GastosApp{-Hom}` + `logs:CreateLogStream`/`PutLogEvents` — **sem**
  `cognito-idp:*` (o trigger não chama o Cognito de volta) e **sem**
  `ssm:GetParametersByPath`/Parameter Store algum (decidido: o nome da
  tabela vem só da variável de ambiente `DynamoDb__TableName`, definida
  direto no bloco `environment{}` do `aws_lambda_function`, lida
  manualmente — mesmo padrão já adotado para `DynamoDbOptions`/
  `CognitoOptions` por causa do binding via `services.Configure<T>()`
  falhar silenciosamente sob Native AOT, ver `backend/infra/CLAUDE.md`.
  Menos IAM e uma chamada de rede a menos no cold start)
- 1 `aws_lambda_permission` por ambiente, concedendo
  `lambda:InvokeFunction` ao principal `cognito-idp.amazonaws.com`,
  `source_arn = aws_cognito_user_pool.main.arn`

**Recursos existentes modificados:**
- `aws_cognito_user_pool.main` (`cognito.tf`, hom e prod): novo bloco
  `lambda_config { post_confirmation = aws_lambda_function.account_trigger.arn }`
- IAM Role `gastosapp-backend-cicd` (`infra/terraform/cicd/`): política
  ampliada para `lambda:UpdateFunctionCode`/
  `UpdateFunctionConfiguration` também no novo Lambda (hoje só cobre
  `gastos-app-api{-hom}`)
- `.github/workflows/`: dois workflows de deploy novos (ou os
  existentes ganham um segundo job), path-filtrados por projeto —
  detalhamento fica pro `tasks.md`/implementação, não é recurso AWS em
  si

**Sem mudança:** tabela `GastosApp` e seus índices (`GSI1`/`GSI2`) —
reaproveitados como já provisionados. Nenhum novo App Client, nenhum
novo parâmetro de Parameter Store além do que a extensão já lê hoje.

## 5. Erros de negócio → `ErrorType`/HTTP

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `Category`/`Expense`: conta não resolvível pro `userId` do JWT (dado corrompido/manual — não deveria ocorrer em uso normal) | `account-not-found` | `Unauthorized` | 401 |

Único erro de negócio novo desta feature — todo o resto (criação de
conta) é sempre "melhor esforço", nunca reportado como erro pro
chamador (ver decisão técnica 2). Nenhuma mudança nos mapeamentos já
existentes de `Category`/`Expense`/`Auth`.

## 6. Testes (visão geral — detalhamento fica pro `tasks.md`)

- `ComponentTestWebApplicationFactory`: novo
  `IAccountRepository AccountRepositoryMock` (+ `Reset...`), mesmo
  padrão de `CategoryRepositoryMock`/`ExpenseRepositoryMock`.
- Novo `ComponentTests` para `EnsureAccountCommand` (cria quando não
  existe, idempotente quando já existe, resolve corretamente sob
  simulação de conflito).
- Novo `ComponentTests`/`UnitTests` para `GastosApp.CognitoTriggers`
  (`Function.cs` invocado diretamente com evento construído em
  memória — sucesso, e falha do `EnsureAccountCommand` não propaga).
- `CategoryEndpointsTests`/`ExpenseEndpointsTests` existentes:
  precisam simular `AccountRepositoryMock`/`ResolveAccountIdQuery`
  retornando um `accountId` de teste para continuar passando (hoje
  presumem resolução implícita por `userId`).
- Atualizar `backend/docs/openapi.json`: sem mudança de contrato
  esperada (regenerar só para confirmar ausência de diff, por exigência
  da constitution).

## Pontos a confirmar antes do `/tasks`

1. ~~Nome definitivo dos recursos Terraform~~ — **decidido**:
   `jrnexpenses-account-trigger` (prefixo `jrnexpenses`, não
   `gastos-app` como os recursos existentes da API/Cognito — só este
   recurso novo usa o prefixo novo; os já existentes não são
   renomeados nesta feature).
2. ~~Permissão de Parameter Store no trigger~~ — **decidido**: variável
   de ambiente direta (`DynamoDb__TableName`), sem SSM. Sem
   `ssm:GetParametersByPath` na IAM Role do trigger (seção 4) e sem
   `AddAwsParameterStore` no `Function.cs` (seção 1).
3. ~~`CancellationToken` no handler do trigger~~ — **confirmado**:
   `CancellationToken.None`, sem `CancellationTokenSource` amarrado a
   `context.RemainingTime`.
