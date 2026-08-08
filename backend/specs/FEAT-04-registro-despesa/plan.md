# FEAT-04: Registro de Despesa — Plano Técnico

## Contexto técnico

Esta é a primeira feature do projeto que persiste dados no DynamoDB (Auth usa
exclusivamente Cognito) e a primeira que introduz uma entidade de `Domain`
de fato (hoje `GastosApp.Domain` está vazio). O plano segue o modelo de
dados já definido em `backend/docs/data-model.md` (item "Transação") e o
access pattern AP1 de `backend/docs/architecture.md`.

## Camadas afetadas

### Domain (`GastosApp.Domain`)
Novo — primeira entidade real do projeto.
- `Expenses/Expense.cs`: entidade `Expense` (sealed class) com `Id`,
  `UserId`, `Description`, `AmountInCents` (long), `Category`
  (`ExpenseCategory`), `ExpenseDate` (`DateOnly`), `CreatedAt`
  (`DateTimeOffset`). Construtor privado + factory `Expense.Create(userId,
  description, amountInCents, category, expenseDate)` que gera `Id`
  (`Guid.NewGuid().ToString()`) e `CreatedAt` (`DateTimeOffset.UtcNow`).
  Sem validação de negócio aqui (já feita no Handler antes de chamar
  `Create`) — a entidade assume que os dados recebidos são válidos.
- `Expenses/ExpenseCategory.cs`: enum fechado —
  `Alimentacao, Transporte, Moradia, Saude, Educacao, Lazer,
  ComprasEServicos, Outros`.

### Application (`GastosApp.Application`)
- `Common/Interfaces/IExpenseRepository.cs`:
  `Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);`
  Sem `Result` de retorno: não há nenhuma falha de negócio esperada nessa
  operação (o `Id` é gerado como UUID, não há conflito possível); qualquer
  falha de infraestrutura (DynamoDB indisponível, etc.) é uma exceção não
  mapeada que sobe para o `GlobalExceptionHandler` → 500, seguindo o mesmo
  comportamento já coberto pelo teste
  `Register_QuandoAuthServiceLancaExcecaoNaoPrevista_Retorna500`.
- `Expenses/ExpenseErrors.cs` (mesmo padrão de `Auth/AuthErrors.cs`):
  `public static Error Validation(string message) => Error.Validation("validation-error", message);`
  (usa o slug `validation-error`, conforme documentado em
  `spec.md`/Contratos da API — diferente do slug `bad-request` usado hoje
  em Auth; cada módulo define seu próprio código de erro de validação).
- `Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs`:
  ```csharp
  public sealed record RegisterExpenseCommand(
      string UserId,
      string Description,
      long AmountInCents,
      string Category,
      DateOnly ExpenseDate) : ICommand<Result<RegisterExpenseResult>>;

  public sealed class RegisterExpenseCommandHandler
      : ICommandHandler<RegisterExpenseCommand, Result<RegisterExpenseResult>>
  {
      private readonly IExpenseRepository _expenseRepository;

      public async ValueTask<Result<RegisterExpenseResult>> Handle(
          RegisterExpenseCommand command, CancellationToken cancellationToken)
      {
          // validações (ver "Mapeamento de erros") ...
          var category = ...; // Enum.TryParse<ExpenseCategory>
          var expense = Expense.Create(command.UserId, command.Description,
              command.AmountInCents, category, command.ExpenseDate);

          await _expenseRepository.SaveAsync(expense, cancellationToken);

          return Result.Success(new RegisterExpenseResult(
              expense.Id, expense.Description, expense.AmountInCents,
              expense.Category.ToString(), expense.ExpenseDate, expense.CreatedAt));
      }
  }

  public record RegisterExpenseResult(
      string Id, string Description, long AmountInCents,
      string Category, DateOnly ExpenseDate, DateTimeOffset CreatedAt);
  ```

### Infrastructure (`GastosApp.Infrastructure`)
- `Configuration/DynamoDbOptions.cs`: POCO refletindo o Parameter Store
  (`/GastosApp/DynamoDb/TableName`, `/GastosApp/DynamoDb/Region`), mesmo
  padrão de `CognitoOptions`.
  ```csharp
  public sealed class DynamoDbOptions
  {
      public const string SectionName = "DynamoDb";
      public string TableName { get; init; } = "GastosApp";
      public string Region { get; init; } = "us-east-1";
  }
  ```
- `Expenses/DynamoDbExpenseRepository.cs`: implementa `IExpenseRepository`
  usando `IAmazonDynamoDB.PutItemAsync` (baixo nível, sem `Scan`, conforme
  regra imutável da constitution). Monta o item conforme
  `backend/docs/data-model.md`:
  - `PK` = `USER#<userId>`
  - `SK` = `TXN#<yyyy-MM>#<id>` (mês extraído de `ExpenseDate`)
  - `GSI1PK` = `USER#<userId>#<category>`
  - `GSI1SK` = `<yyyy-MM>#<id>`
  - Atributos: `Description`, `AmountInCents` (N), `Category` (S),
    `ExpenseDate` (S, ISO 8601), `Tipo` = `"despesa"` (S, fixo — já
    reservando o atributo para receitas futuras), `CreatedAt` (S, ISO 8601)
  - Sem `ConditionExpression` de unicidade: `Id` é UUID, colisão
    estatisticamente impossível — mesmo racional de não tratar isso como
    erro de negócio na Application.

### Api (`GastosApp.Api`)
- `Endpoints/ExpenseEndpoints.cs`, seguindo o mesmo formato de
  `AuthEndpoints.cs`:
  ```csharp
  var group = app.MapGroup("/expenses").WithTags("Expenses").RequireAuthorization();
  group.MapPost("/", RegisterExpense);
  ```
  Handler extrai `userId` do `ClaimsPrincipal` (claim `sub`, mesmo padrão
  de `AuthEndpoints.UserData`), monta `RegisterExpenseCommand`, envia via
  `ISender` e mapeia o resultado com `ResultHttpExtensions.ToHttpResult`
  para `Results.Created($"/expenses/{value.Id}", value)`.
  `RequireAuthorization()` no grupo já garante 401 automático (via
  `OnChallenge` do JWT Bearer configurado em
  `Infrastructure/Extensions/AddCognitoSdk.cs`) sem código adicional —
  mesmo mecanismo que produz o 401 padronizado hoje.
- `RegisterExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate)`
  como DTO de request (record, mesmo padrão de `RegisterRequest`/`LoginRequest`).
- `Program.cs`: adicionar `app.MapExpenseEndpoints();` após `app.MapAuthEndpoints();`.
- `InfrastructureServiceCollectionExtensions.AddAwsInfrastructure`:
  substituir o comentário `// No futuro, suas injeções do DynamoDB...` por:
  ```csharp
  services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));
  services.AddSingleton<IAmazonDynamoDB>(sp =>
      new AmazonDynamoDBClient(RegionEndpoint.GetBySystemName(configuration["DynamoDb:Region"] ?? regionStr)));
  ```
  e em `AddInfrastructure`, registrar `services.AddScoped<IExpenseRepository, DynamoDbExpenseRepository>();`.

## Validações no Handler (antes de construir `Expense`)

| Campo | Regra | Erro |
|---|---|---|
| `Description` | `IsNullOrWhiteSpace` → inválido; `Length > 200` → inválido | `ExpenseErrors.Validation("Descrição é obrigatória.")` / `"Descrição deve ter no máximo 200 caracteres."` |
| `AmountInCents` | `<= 0` → inválido | `ExpenseErrors.Validation("Valor deve ser maior que zero.")` |
| `Category` | `Enum.TryParse<ExpenseCategory>(command.Category, ignoreCase: true, out ...)` falha → inválido | `ExpenseErrors.Validation("Categoria inválida.")` |
| `ExpenseDate` | Nenhuma validação de negócio adicional (retroativa e futura são permitidas, conforme spec); a validação de formato ISO 8601 é responsabilidade do model binding do Minimal API (`DateOnly`) — payload malformado já retorna 400 automaticamente antes de chegar ao Handler | — |

`UserId` não é validado no Handler (garantidamente não vazio, pois vem do
JWT autenticado — o próprio `RequireAuthorization()` impede a requisição de
chegar ao endpoint sem um `sub` válido).

## Mapeamento de erros → `ErrorType` → HTTP

| Cenário | `ErrorType` | Status HTTP | `type` (slug) |
|---|---|---|---|
| Descrição ausente/vazia/> 200 chars | `Validation` | 400 | `validation-error` |
| Valor <= 0 | `Validation` | 400 | `validation-error` |
| Categoria fora do enum | `Validation` | 400 | `validation-error` |
| Sem token / token inválido | — (tratado no middleware JWT, não no Handler) | 401 | `unauthorized` |
| Falha inesperada (DynamoDB indisponível, etc.) | — (exceção não mapeada → `GlobalExceptionHandler`) | 500 | `internal-server-error` |

## Recursos AWS afetados

- **Nenhuma tabela nova**: usa a tabela `GastosApp` (single-table) e o
  índice `GSI1`, ambos já definidos em `backend/docs/architecture.md`.
  Se ainda não existem provisionados manualmente na conta AWS, precisam
  ser criados antes dos testes manuais/integração (fora do escopo deste
  plano — IaC via Terraform é trabalho futuro, conforme `constitution.md`).
- **Novo parâmetro no Parameter Store**: `/GastosApp/DynamoDb/TableName`
  e `/GastosApp/DynamoDb/Region` (ou usar defaults `"GastosApp"` /
  `"us-east-1"` caso os parâmetros ainda não existam — `DynamoDbOptions`
  já traz esses defaults).
- Nenhum novo recurso do Cognito.

## Plano de Testes de Componente

Segue o padrão de `FEAT-03-testes-componentes/spec.md`: novo dublê
`IExpenseRepository` exposto em `ComponentTestWebApplicationFactory`
(`ExpenseRepositoryMock`, com `ResetExpenseRepositoryMock()`), novo arquivo
`Expenses/ExpenseEndpointsTests.cs`.

- `POST /expenses`
  - Sucesso: usuário autenticado (via `TestAuthHandler`) + body válido →
    `ExpenseRepositoryMock.SaveAsync` configurado para completar
    normalmente; espera 201, `Location: /expenses/{id}`, corpo com os
    campos da despesa criada.
  - Sem header de autenticação → espera 401 (`type` = `.../unauthorized`),
    `ExpenseRepositoryMock.SaveAsync` **não** deve ser chamado.
  - Descrição vazia/ausente → 400 (`type` = `.../validation-error`), sem
    chamar `SaveAsync`.
  - Valor <= 0 → 400 (`type` = `.../validation-error`), sem chamar `SaveAsync`.
  - Categoria fora do enum → 400 (`type` = `.../validation-error`), sem
    chamar `SaveAsync`.
  - Data retroativa e data futura → ambas aceitas normalmente (201).
  - Dois usuários diferentes (dois headers `TestAuthHandler` distintos) →
    cada requisição chama `SaveAsync` com o `Expense.UserId` correspondente
    ao token usado (verificado via `Received()` do NSubstitute, checando o
    argumento capturado).
  - Smoke test de falha inesperada: `SaveAsync` configurado para lançar
    exceção → espera 500 (`type` = `.../internal-server-error`), reforçando
    o comportamento já coberto para Auth.

## Pontos que precisam de confirmação antes do `/tasks`

1. **Lista de categorias do enum** já foi validada na spec (`Alimentacao,
   Transporte, Moradia, Saude, Educacao, Lazer, ComprasEServicos, Outros`)
   — confirmar se pode virar `ExpenseCategory` literalmente com esses
   nomes (sem acento, PascalCase) ou se há preferência de nomenclatura.
2. **Slug de erro de validação**: proponho `validation-error` (documentado
   na spec) em vez de reaproveitar `bad-request` (usado em Auth) — confirmar
   que módulos diferentes podem ter slugs de erro de validação distintos,
   ou se deveria haver um slug único e compartilhado para todo o projeto.
3. **Tabela/índice DynamoDB `GastosApp`/`GSI1`**: confirmar se já existem
   provisionados manualmente na conta AWS, ou se isso precisa ser criado
   como pré-requisito manual antes de rodar a feature localmente contra a
   AWS real (a criação da tabela em si está fora do escopo deste plano/FEAT).
