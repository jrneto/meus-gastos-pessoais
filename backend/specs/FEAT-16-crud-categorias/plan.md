# Plan: FEAT-16 — CRUD de Categorias — Plano Técnico

## Contexto técnico

`spec.md` já foi aprovado: `GET/POST/PUT/DELETE /categories`, entidade
`Categoria` isolada de `Expense` (nenhuma mudança em `POST`/`GET
/expenses` ou no enum `ExpenseCategory`), sem seed automático de
categorias padrão (adiado), e exclusão bloqueada com 422 quando existir
despesa com `category` igual ao `nome` da categoria.

Duas decisões técnicas centrais moldam este plano:

1. **Unicidade de `nome` por usuário via slug, sem `Scan`**: a `SK` do
   item `Categoria` é derivada de um slug do nome
   (`CAT#{slug}`) — minúsculo, sem acento/caractere especial, espaços
   viram `-` (ex.: `"Compras e Serviços"` → `compras-e-servicos`).
   Isso transforma a checagem de duplicidade em uma escrita condicional
   (`attribute_not_exists(PK)`) na própria chave primária — nenhuma
   `Query`/`Scan` extra é necessária para validar unicidade, só a
   captura de `ConditionalCheckFailedException`. O atributo `Nome`
   gravado no item preserva a grafia original enviada pelo cliente (o
   slug existe só na chave). Ver `CategorySlug` (Domain-layer) abaixo.
2. **Reaproveitar os índices já existentes (`GSI1`, `GSI2`), nenhum
   recurso novo**: `GSI2` (`GSI2PK = ID#{id}`, `KEYS_ONLY`, criado na
   FEAT-07 para despesas) resolve `PUT`/`DELETE /categories/{id}` a
   partir do `id` opaco, mesmo padrão já usado em
   `DynamoDbExpenseRepository`. `GSI1` (`GSI1PK = USER#{userId}#{categoria}`,
   já usado para o filtro `category` de `GET /expenses` na FEAT-06)
   resolve a checagem "existe despesa nessa categoria?" do bloqueio de
   exclusão (US10) com uma única `Query` (`Limit = 1`) na mesma chave —
   sem tocar em `Expense`/`DynamoDbExpenseRepository` além de adicionar
   esse método de leitura.

Renomear uma categoria (`PUT` mudando `nome`) muda a `SK` — mesmo
desafio já resolvido na FEAT-08 para `expenseDate`: usa
`TransactWriteItems` (`Delete` do item antigo + `Put` condicional do
novo) quando o nome muda, `PutItem` simples quando não muda.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Novo `Category` (`GastosApp.Domain.Categories`), mirror de `Expense`: `Create`/`Restore`, imutável |
| Application | Novos `CreateCategoryCommand`, `UpdateCategoryCommand`, `DeleteCategoryCommand`, `GetCategoriesQuery` (+ Handlers, Validators, Results); `CategoryErrors`; `ICategoryRepository` (novo); `IExpenseRepository` ganha `ExistsByCategoryAsync`; `Error`/`ErrorType` ganham `UnprocessableEntity` (422) |
| Infrastructure | Novo `DynamoDbCategoryRepository`; `DynamoDbExpenseRepository` ganha `ExistsByCategoryAsync` (Query no `GSI1` já existente) |
| Api | Novo `CategoryEndpoints.cs` (`GET`/`POST`/`PUT`/`DELETE /categories`); `AppJsonSerializerContext` ganha os novos DTOs; `ResultHttpExtensions` mapeia `UnprocessableEntity` → 422; `Program.cs` registra `MapCategoryEndpoints()` |
| AWS/Terraform | Nenhum recurso novo — reaproveita a tabela `GastosApp` e os índices `GSI1`/`GSI2` já provisionados (hom e prod) |

## Domain-layer

`backend/src/GastosApp.Domain/Categories/Category.cs` (novo, mirror de `Expense.cs`):

```csharp
public sealed class Category
{
    public string Id { get; }
    public string UserId { get; }
    public string Nome { get; }
    public string Cor { get; }
    public string Icone { get; }
    public DateTimeOffset CreatedAt { get; }

    private Category(string id, string userId, string nome, string cor, string icone, DateTimeOffset createdAt)
    {
        Id = id; UserId = userId; Nome = nome; Cor = cor; Icone = icone; CreatedAt = createdAt;
    }

    public static Category Create(string userId, string nome, string cor, string icone) =>
        new(Guid.NewGuid().ToString(), userId, nome, cor, icone, DateTimeOffset.UtcNow);

    public static Category Restore(string id, string userId, string nome, string cor, string icone, DateTimeOffset createdAt) =>
        new(id, userId, nome, cor, icone, createdAt);
}
```

Sem enum/value object para `Cor`/`Icone` — validação de formato fica no
`Validator` (FluentValidation), igual ao padrão de `Description`/
`AmountInCents` em `Expense`.

### `CategorySlug` (novo: `backend/src/GastosApp.Domain/Categories/CategorySlug.cs`)

Função pura, sem dependência de infraestrutura — decide quando dois
nomes de categoria "são o mesmo" para fins de unicidade. Usada pela
`Infrastructure` para montar a `SK` e pela `Validator` para rejeitar
nomes que normalizam para slug vazio.

```csharp
public static class CategorySlug
{
    public static string From(string nome)
    {
        var normalized = nome.Trim().ToLowerInvariant();
        normalized = RemoveDiacritics(normalized);
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");   // remove tudo que não é letra/dígito/espaço/hífen
        normalized = Regex.Replace(normalized, @"[\s-]+", "-").Trim('-'); // colapsa espaços/hífens repetidos em um único "-"
        return normalized;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
```

Exemplos: `"Compras e Serviços"` → `compras-e-servicos`;
`"Lazer"`/`"  lazer  "`/`"LAZER"` → `lazer` (todos colidem — 422 no
segundo); `"Compras  e  Serviços"` (espaços duplos) → `compras-e-servicos`
(mesmo slug de `"Compras e Serviços"` — agora colide, diferente do
comportamento com trim+lowercase puro discutido antes).

**Caso extremo — slug vazio**: um nome composto só de caracteres
removidos pela normalização (ex.: `"!!!"`, `"???"`, só emojis) produz
slug `""`, o que geraria `SK = "CAT#"` — colide com o prefixo usado por
`begins_with(SK, "CAT#")` em `ListAsync` e é ambíguo entre diferentes
nomes "vazios". `CreateCategoryCommandValidator`/`UpdateCategoryCommandValidator`
ganham uma regra extra: `Must(nome => CategorySlug.From(nome).Length > 0)`
com mensagem "Nome deve conter ao menos uma letra ou número." — rejeita
esse caso com 400 antes de chegar ao repositório.

## Application-layer

### `Error`/`ErrorType` — novo tipo 422 (`backend/src/GastosApp.Application/Common/Results/`)

```csharp
public enum ErrorType { Validation, Conflict, Unauthorized, NotFound, UnprocessableEntity, Failure }

public static Error UnprocessableEntity(string code, string message) =>
    new(code, message, ErrorType.UnprocessableEntity);
```

`Conflict` (409) já existe e é usado por `AuthErrors.EmailAlreadyExists`
— não reaproveitado aqui porque `spec.md` fixa 422 (RFC 9457/4918,
"regra de negócio violada por um recurso identificável", diferente de
409 "conflito de estado concorrente"). `UnprocessableEntity` é o novo
case a mapear em `ResultHttpExtensions.BuildProblem`
(`StatusCodes.Status422UnprocessableEntity`).

### `CategoryErrors` (novo: `backend/src/GastosApp.Application/Categories/CategoryErrors.cs`)

```csharp
public static class CategoryErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Categoria não encontrada.");
    public static Error NameConflict => Error.UnprocessableEntity("name-conflict", "Já existe uma categoria com esse nome.");
    public static Error CategoryInUse => Error.UnprocessableEntity(
        "category-in-use", "A categoria não pode ser excluída enquanto houver despesas associadas a ela.");
}
```

### `ICategoryRepository` (novo: `backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs`)

```csharp
public enum CategoryWriteOutcome { Success, NotFound, NameConflict }

public sealed record CategoryWriteResult(CategoryWriteOutcome Outcome, Category? Category)
{
    public static CategoryWriteResult Success(Category category) => new(CategoryWriteOutcome.Success, category);
    public static CategoryWriteResult NotFound() => new(CategoryWriteOutcome.NotFound, null);
    public static CategoryWriteResult NameConflict() => new(CategoryWriteOutcome.NameConflict, null);
}

public interface ICategoryRepository
{
    Task<CategoryWriteResult> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(string userId, string categoryId, CancellationToken cancellationToken = default);
    Task<CategoryWriteResult> UpdateAsync(
        string userId, string categoryId, string nome, string cor, string icone,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string categoryId, CancellationToken cancellationToken = default);
}
```

`CategoryWriteResult` existe porque `Create`/`Update` têm três desfechos
possíveis (sucesso / não encontrado — só `Update` / nome duplicado), o
que um `Category?` simples (padrão usado em `Expense`, só
sucesso-ou-não-encontrado) não expressa. Handlers traduzem
`CategoryWriteOutcome` para `Error`/status HTTP — repositório nunca
lança exceção para fluxo de negócio (regra da constitution).

### `IExpenseRepository` — novo método (`backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs`)

```csharp
Task<bool> ExistsByCategoryAsync(string userId, string category, CancellationToken cancellationToken = default);
```

Único ponto de acoplamento entre os dois módulos, exigido pela regra de
negócio "não excluir categoria com despesas associadas" (US10). Não
altera nenhum método existente.

### Commands/Queries (novos, mirror exato do padrão de `Expenses/Commands/*`)

```csharp
// Commands/CreateCategory/CreateCategoryCommand.cs
public sealed record CreateCategoryCommand(string UserId, string Nome, string Cor, string Icone)
    : ICommand<Result<CreateCategoryResult>>;

public sealed class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, Result<CreateCategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;
    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository) => _categoryRepository = categoryRepository;

    public async ValueTask<Result<CreateCategoryResult>> Handle(CreateCategoryCommand command, CancellationToken ct)
    {
        var category = Category.Create(command.UserId, command.Nome, command.Cor, command.Icone);
        var result = await _categoryRepository.CreateAsync(category, ct);

        return result.Outcome switch
        {
            CategoryWriteOutcome.Success => Result.Success(CreateCategoryResult.FromEntity(result.Category!)),
            _ => Result.Failure<CreateCategoryResult>(CategoryErrors.NameConflict)
        };
    }
}

public record CreateCategoryResult(string Id, string Nome, string Cor, string Icone, DateTimeOffset CreatedAt)
{
    public static CreateCategoryResult FromEntity(Category category) =>
        new(category.Id, category.Nome, category.Cor, category.Icone, category.CreatedAt);
}
```

```csharp
// Commands/UpdateCategory/UpdateCategoryCommand.cs
public sealed record UpdateCategoryCommand(string UserId, string CategoryId, string Nome, string Cor, string Icone)
    : ICommand<Result<UpdateCategoryResult>>;

public sealed class UpdateCategoryCommandHandler : ICommandHandler<UpdateCategoryCommand, Result<UpdateCategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;
    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository) => _categoryRepository = categoryRepository;

    public async ValueTask<Result<UpdateCategoryResult>> Handle(UpdateCategoryCommand command, CancellationToken ct)
    {
        var result = await _categoryRepository.UpdateAsync(
            command.UserId, command.CategoryId, command.Nome, command.Cor, command.Icone, ct);

        return result.Outcome switch
        {
            CategoryWriteOutcome.Success => Result.Success(UpdateCategoryResult.FromEntity(result.Category!)),
            CategoryWriteOutcome.NotFound => Result.Failure<UpdateCategoryResult>(CategoryErrors.NotFound),
            _ => Result.Failure<UpdateCategoryResult>(CategoryErrors.NameConflict)
        };
    }
}

public record UpdateCategoryResult(string Id, string Nome, string Cor, string Icone, DateTimeOffset CreatedAt)
{
    public static UpdateCategoryResult FromEntity(Category category) =>
        new(category.Id, category.Nome, category.Cor, category.Icone, category.CreatedAt);
}
```

```csharp
// Commands/DeleteCategory/DeleteCategoryCommand.cs
public sealed record DeleteCategoryCommand(string UserId, string CategoryId) : ICommand<Result>;

public sealed class DeleteCategoryCommandHandler : ICommandHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IExpenseRepository _expenseRepository;

    public DeleteCategoryCommandHandler(ICategoryRepository categoryRepository, IExpenseRepository expenseRepository)
    {
        _categoryRepository = categoryRepository;
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result> Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(command.UserId, command.CategoryId, ct);
        if (category is null)
            return Result.Failure(CategoryErrors.NotFound);

        var inUse = await _expenseRepository.ExistsByCategoryAsync(command.UserId, category.Nome, ct);
        if (inUse)
            return Result.Failure(CategoryErrors.CategoryInUse);

        var deleted = await _categoryRepository.DeleteAsync(command.UserId, command.CategoryId, ct);
        return deleted ? Result.Success() : Result.Failure(CategoryErrors.NotFound);
    }
}
```
`GetByIdAsync` antes do `DeleteAsync` é necessário para obter o `Nome`
atual da categoria (a checagem de despesas associadas é por nome, não
por id) — o `DeleteAsync` final ainda existe para lidar com a corrida
rara de exclusão concorrente entre as duas chamadas (mesma idempotência
já aceita em `DeleteExpenseCommandHandler`).

```csharp
// Queries/GetCategories/GetCategoriesQuery.cs
public sealed record GetCategoriesQuery(string UserId) : IQuery<Result<GetCategoriesResult>>;

public sealed class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, Result<GetCategoriesResult>>
{
    private readonly ICategoryRepository _categoryRepository;
    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository) => _categoryRepository = categoryRepository;

    public async ValueTask<Result<GetCategoriesResult>> Handle(GetCategoriesQuery query, CancellationToken ct)
    {
        var categories = await _categoryRepository.ListAsync(query.UserId, ct);
        return Result.Success(GetCategoriesResult.FromEntities(categories));
    }
}

public sealed record GetCategoriesResult(IReadOnlyList<CategorySummary> Items)
{
    public static GetCategoriesResult FromEntities(IReadOnlyList<Category> categories) =>
        new(categories.Select(CategorySummary.FromEntity).ToList());
}

public sealed record CategorySummary(string Id, string Nome, string Cor, string Icone, DateTimeOffset CreatedAt)
{
    public static CategorySummary FromEntity(Category category) =>
        new(category.Id, category.Nome, category.Cor, category.Icone, category.CreatedAt);
}
```

### Validators (novos, mirror de `RegisterExpenseCommandValidator`)

```csharp
public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNomeLength = 50;
    private const int MaxIconeLength = 50;
    private static readonly Regex HexColor = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public CreateCategoryCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Nome).NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(MaxNomeLength).WithMessage($"Nome deve ter no máximo {MaxNomeLength} caracteres.")
            .Must(nome => CategorySlug.From(nome).Length > 0)
                .WithMessage("Nome deve conter ao menos uma letra ou número.");
        RuleFor(c => c.Cor).NotEmpty().WithMessage("Cor é obrigatória.")
            .Matches(HexColor).WithMessage("Cor deve estar no formato hexadecimal #RRGGBB.");
        RuleFor(c => c.Icone).NotEmpty().WithMessage("Ícone é obrigatório.")
            .MaximumLength(MaxIconeLength).WithMessage($"Ícone deve ter no máximo {MaxIconeLength} caracteres.");
    }
}

public sealed class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    // Mesmas três regras de CreateCategoryCommandValidator (Nome/Cor/Icone); CategoryId não é
    // validado — vem do path, sempre presente pela própria rota (mesmo padrão de UpdateExpenseCommandValidator).
}
```

## Infrastructure-layer — `DynamoDbCategoryRepository` (novo)

Item model:

| Atributo | Valor |
|---|---|
| `PK` | `USER#{userId}` |
| `SK` | `CAT#{CategorySlug.From(nome)}` |
| `GSI2PK` | `ID#{id}` |
| `Nome` | grafia original enviada pelo cliente |
| `Cor`, `Icone`, `CreatedAt` | atributos regulares |

```csharp
public async Task<CategoryWriteResult> CreateAsync(Category category, CancellationToken ct = default)
{
    var item = BuildItem(category);
    try
    {
        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _options.TableName,
            Item = item,
            ConditionExpression = "attribute_not_exists(PK)"   // SK já é o slug, garante unicidade case/acento/espaço-insensitive
        }, ct);
        return CategoryWriteResult.Success(category);
    }
    catch (ConditionalCheckFailedException)
    {
        return CategoryWriteResult.NameConflict();
    }
}

public async Task<IReadOnlyList<Category>> ListAsync(string userId, CancellationToken ct = default)
{
    var response = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new AttributeValue { S = $"USER#{userId}" },
            [":skPrefix"] = new AttributeValue { S = "CAT#" }
        }
    }, ct);

    return response.Items.Select(MapToCategory).ToList();
}

public async Task<Category?> GetByIdAsync(string userId, string categoryId, CancellationToken ct = default)
{
    // Query no GSI2 (GSI2PK = ID#{categoryId}, Limit 1) → checa posse (PK == USER#{userId}) →
    // GetItem(PK, SK) para o item completo. Mesmo padrão de DynamoDbExpenseRepository.GetByIdAsync.
}

public async Task<CategoryWriteResult> UpdateAsync(
    string userId, string categoryId, string nome, string cor, string icone, CancellationToken ct = default)
{
    // 1. Query GSI2 por GSI2PK = ID#{categoryId} (Limit 1) → PK/SK reais; posse != USER#{userId} → NotFound
    // 2. newSk = CAT#{CategorySlug.From(nome)}
    // 3. newSk == oldSk (slug não mudou, ainda que a grafia enviada seja diferente):
    //      PutItem simples sobrescrevendo Nome/Cor/Icone (não precisa condition, é o mesmo item)
    // 4. newSk != oldSk (renomeou):
    //      TransactWriteItems: Delete(PK, oldSk, ConditionExpression: attribute_exists(PK))
    //                         + Put(PK, newSk, ConditionExpression: attribute_not_exists(PK))
    //      TransactWriteItemsAsync lança TransactionCanceledException se o Put falhar por
    //      attribute_not_exists (nome já usado por outra categoria) — CancellationReasons[1].Code ==
    //      "ConditionalCheckFailed" identifica qual dos dois itens causou a falha → NameConflict;
    //      se for o Delete (item sumiu entre a Query e aqui) → NotFound
    // Retorna CategoryWriteResult.Success(Category.Restore(...)) preservando CreatedAt do item original
}

public async Task<bool> DeleteAsync(string userId, string categoryId, CancellationToken ct = default)
{
    // Idêntico a DynamoDbExpenseRepository.DeleteAsync: Query GSI2 → checagem de posse →
    // DeleteItem condicional (attribute_exists(PK)), captura ConditionalCheckFailedException → false
}
```

`TransactWriteItems` para o caso de renomear já é padrão estabelecido na
FEAT-08 (`DynamoDbExpenseRepository.UpdateAsync` quando `expenseDate`
muda) — mesma técnica, diferente é que aqui o `Put` também é
condicional (`attribute_not_exists`) para impedir a corrida "duas
renomeações simultâneas para o mesmo nome", o que a FEAT-08 não
precisava (data não tem conceito de duplicidade).

### `DynamoDbExpenseRepository.ExistsByCategoryAsync` (novo método, mesmo arquivo da FEAT-04/06/07/08)

```csharp
public async Task<bool> ExistsByCategoryAsync(string userId, string category, CancellationToken ct = default)
{
    var response = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        IndexName = Gsi1Index,
        KeyConditionExpression = "GSI1PK = :gsi1pk",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":gsi1pk"] = new AttributeValue { S = $"USER#{userId}#{category}" }
        },
        Limit = 1
    }, ct);

    return response.Items.Count > 0;
}
```
Comparação exata (case-sensitive) contra o valor gravado em
`Expense.Category` (`ToString()` do enum `ExpenseCategory`, ex.:
`"Alimentacao"`) — consistente com a regra de negócio do `spec.md`. Para
uma categoria criada pelo cliente com nome fora do enum (ex.: "Viagem"),
sempre retorna `false` (nenhuma despesa pode ter essa categoria hoje,
já que `Expense.category` só aceita o enum fechado) — exclusão nunca é
bloqueada para essas.

## Api-layer

`backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs` (novo, mirror
de `ExpenseEndpoints.cs`):

```csharp
var group = app.MapGroup("/categories")
    .WithTags("Categories")
    .RequireAuthorization()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

group.MapGet("/", GetCategories)
    .Produces<GetCategoriesResult>(StatusCodes.Status200OK);

group.MapPost("/", CreateCategory)
    .Produces<CreateCategoryResult>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

group.MapPut("/{id}", UpdateCategory)
    .Produces<UpdateCategoryResult>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

group.MapDelete("/{id}", DeleteCategory)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
```

Handlers seguem exatamente o padrão de `ExpenseEndpoints` (`userId` do
`ClaimsPrincipal`, `sender.Send`, `result.ToHttpResult(...)`).
`CreateCategoryRequest`/`UpdateCategoryRequest` são `record(string Nome,
string Cor, string Icone)`, mesmo shape do `POST`/`PUT` do contrato.

### `ResultHttpExtensions.BuildProblem` — novo case

```csharp
ErrorType.UnprocessableEntity => (StatusCodes.Status422UnprocessableEntity, error.Message, (string?)null),
```

### `AppJsonSerializerContext` — novos `[JsonSerializable]`

`CreateCategoryRequest`, `UpdateCategoryRequest`, `CreateCategoryResult`,
`UpdateCategoryResult`, `GetCategoriesResult`, `CategorySummary`.

### `Program.cs`

`app.MapCategoryEndpoints();` ao lado de `app.MapExpenseEndpoints();`.

### DI

- `InfrastructureServiceCollectionExtensions.AddInfrastructure`:
  `services.AddScoped<ICategoryRepository, DynamoDbCategoryRepository>();`
- `ApplicationServiceCollectionExtensions.AddApplicationServices`: nada
  a mudar — `AddValidatorsFromAssembly`/`AddMediator` já descobrem os
  novos Commands/Queries/Validators automaticamente pela assembly scan.

## Mapeamento de erros

| Cenário | `ErrorType` | Status HTTP | `type` (slug) |
|---|---|---|---|
| `Nome`/`Cor`/`Icone` ausente ou inválido | `Validation` | 400 | `validation-error` |
| Nome duplicado (criação ou renomeio) | `UnprocessableEntity` (novo) | 422 | `name-conflict` |
| Exclusão com despesas associadas | `UnprocessableEntity` (novo) | 422 | `category-in-use` |
| Categoria inexistente ou de outro usuário (`PUT`/`DELETE`) | `NotFound` | 404 | `not-found` |
| Sem token / token inválido | — (middleware Cognito) | 401 | `unauthorized` |
| Falha inesperada (DynamoDB indisponível, transação cancelada por motivo não mapeado) | — (exceção não mapeada → `GlobalExceptionHandler`) | 500 | `internal-server-error` |

## Recursos AWS

Nenhum recurso novo. Reaproveita a tabela `GastosApp` e os índices
`GSI1`/`GSI2` já provisionados em `backend/infra/terraform/environments/{hom,prod}/dynamodb.tf`
(nenhuma mudança em Terraform).

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Domain/CategorySlugTests.cs` — `"Compras e Serviços"` →
  `"compras-e-servicos"`; `"Lazer"`/`"  lazer  "`/`"LAZER"` → mesmo slug
  `"lazer"`; `"Compras  e  Serviços"` (espaços duplos) → mesmo slug de
  `"Compras e Serviços"`; `"!!!"`/só emoji → slug vazio (`""`)
- `Application/CreateCategoryCommandValidatorTests.cs` /
  `UpdateCategoryCommandValidatorTests.cs` — nome vazio/> 50 chars, cor
  fora do formato hex, ícone vazio/> 50 chars, nome que normaliza para
  slug vazio (ex.: `"!!!"`)
- `Application/CreateCategoryCommandHandlerTests.cs` —
  `CategoryWriteOutcome.Success` → `Result.Success`;
  `NameConflict` → `Result.Failure` com `ErrorType.UnprocessableEntity`/`name-conflict`
- `Application/UpdateCategoryCommandHandlerTests.cs` — os três outcomes
  (`Success`/`NotFound`/`NameConflict`) mapeados corretamente
- `Application/DeleteCategoryCommandHandlerTests.cs` —
  `GetByIdAsync` retorna `null` → `NotFound` sem chamar
  `ExistsByCategoryAsync`; `ExistsByCategoryAsync` retorna `true` →
  `CategoryInUse` sem chamar `DeleteAsync`; caminho feliz completo
  chama os três métodos na ordem
- `Application/GetCategoriesQueryHandlerTests.cs` — lista vazia e lista
  populada mapeadas corretamente
- `Infrastructure/DynamoDbCategoryRepositoryTests.cs` (mock de
  `IAmazonDynamoDB` via NSubstitute) — `CreateAsync` com
  `ConditionalCheckFailedException` → `NameConflict`; `UpdateAsync` sem
  mudança de nome → `PutItem` simples; `UpdateAsync` com mudança de nome
  → `TransactWriteItems` com `Delete`+`Put` condicionais; `GetByIdAsync`/
  `DeleteAsync` com item de outro usuário → tratado como não encontrado
- `Infrastructure/DynamoDbExpenseRepositoryExistsByCategoryTests.cs` —
  `Query` no `GSI1` com `GSI1PK` correto; `Items.Count > 0` → `true`

### Component tests (`backend/tests/GastosApp.ComponentTests/Categories/CategoryEndpointsTests.cs`, mockando `ICategoryRepository`/`IExpenseRepository`)

Cobrindo os 12 critérios de aceite do `spec.md`: `GET` vazio (200, lista
vazia) e populado; `POST` sucesso (201) e nome duplicado (422); `POST`
com campos inválidos (400, um teste por campo); `PUT` sucesso (200),
nome duplicado (422), inexistente/de outro usuário (404); `DELETE`
sucesso (204), com despesas associadas (422 + `ExistsByCategoryAsync`
mockado `true`), inexistente/de outro usuário (404); todas as rotas sem
token → 401 sem chamar nenhum repositório.

## Critical Files

- `backend/src/GastosApp.Domain/Categories/Category.cs` (novo)
- `backend/src/GastosApp.Domain/Categories/CategorySlug.cs` (novo)
- `backend/src/GastosApp.Application/Common/Results/ErrorType.cs`,
  `Error.cs` — novo `UnprocessableEntity`
- `backend/src/GastosApp.Application/Categories/CategoryErrors.cs` (novo)
- `backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs` (novo)
- `backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs` — `ExistsByCategoryAsync`
- `backend/src/GastosApp.Application/Categories/Commands/{CreateCategory,UpdateCategory,DeleteCategory}/*.cs` (novos)
- `backend/src/GastosApp.Application/Categories/Queries/GetCategories/GetCategoriesQuery.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs` — `ExistsByCategoryAsync`
- `backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — registrar `ICategoryRepository`
- `backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs` (novo)
- `backend/src/GastosApp.Api/Common/ResultHttpExtensions.cs` — case `UnprocessableEntity`
- `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs` — novos DTOs
- `backend/src/GastosApp.Api/Program.cs` — `MapCategoryEndpoints()`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln` — confirma que os novos
  `ICommand<Result<T>>`/`IQuery<Result<T>>` compilam e o source
  generator do Mediator descobre os novos handlers
- `dotnet test backend/GastosApp.sln` — suíte completa (Unit + Component)
  cobrindo os cenários acima, mais os testes já existentes (não deve
  haver regressão em `Expenses`)
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution para toda mudança de contrato)
- Smoke manual (opcional, contra AWS real): criar categoria, listar,
  editar (mudando só cor, depois mudando o nome), tentar criar duplicata
  (422), registrar uma despesa em `Alimentacao`, tentar excluir uma
  categoria "Alimentacao" criada manualmente (422), excluir a despesa e
  tentar de novo (204)

## Decisões confirmadas

1. **Novo `ErrorType.UnprocessableEntity` (422)** — confirmado. Hoje o
   projeto só tem `Conflict` (409, usado por `AuthErrors.EmailAlreadyExists`);
   este plano adiciona um case novo porque `spec.md` fixa 422
   explicitamente para os dois cenários desta feature.
2. **Unicidade de `nome` via slug (`CategorySlug`), não só
   trim+lowercase** — confirmado. A `SK` do item é derivada de um slug
   que remove acento e caractere especial, colapsa espaços em `-` e
   deixa tudo minúsculo (ex.: `"Compras e Serviços"` →
   `compras-e-servicos`); o atributo `Nome` retornado nas respostas
   preserva a grafia original enviada pelo cliente. Nomes que
   normalizam para slug vazio (ex.: `"!!!"`) são rejeitados com 400 na
   validação, antes de chegar ao repositório (ver `CategorySlug` acima).
3. **`TransactWriteItems` com `Put` condicional no renomeio, usando
   `CancellationReasons` para diferenciar motivo de cancelamento** —
   confirmado. Diferente da FEAT-08 (onde só o `Delete` tinha
   `ConditionExpression`), aqui o `Put` do novo slug também precisa de
   `attribute_not_exists(PK)` para impedir que duas renomeações
   concorrentes colidam no mesmo nome; `CancellationReasons[0]`
   (`Delete`) vs `CancellationReasons[1]` (`Put`) diferencia "item
   sumiu" (404) de "nome já existe" (422) — primeira vez que o projeto
   precisa inspecionar motivos de cancelamento dentro de um
   `TransactWriteItems`.
