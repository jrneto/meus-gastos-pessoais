# Plan: FEAT-17 — Despesas vinculadas à Categoria dinâmica — Plano Técnico

## Contexto técnico

`spec.md` já foi aprovado: `Expense.category` (enum fechado
`ExpenseCategory`) vira `Expense.categoryId` (string, `id` de uma
`Categoria` própria do usuário, entidade da FEAT-16). Decisões já
fechadas: vínculo por `id` (não nome), enum `ExpenseCategory` removido
do projeto, sem seed automático, sem migração de dados (usuário vai
apagar a base manualmente).

Duas mudanças estruturais concentram o trabalho:

1. **Validação de `categoryId` vira uma checagem assíncrona contra o
   `ICategoryRepository`**, não mais um `Enum.TryParse` síncrono. Os
   `Validator`s de `RegisterExpenseCommand`/`UpdateExpenseCommand`
   passam a receber `ICategoryRepository` por injeção de dependência
   (já suportado pelo `ValidationBehavior`, que chama `ValidateAsync` —
   nenhuma mudança no pipeline é necessária) e usam `MustAsync` pra
   confirmar que a categoria existe **e pertence ao mesmo `userId`** do
   comando — os dois casos (inexistente / de outro usuário) caem na
   mesma mensagem de validação, sem diferenciar (US3/US5 do `spec.md`).
2. **`GSI1` (já existente, criado na FEAT-04/06) passa a indexar por
   `categoryId` em vez do nome do enum**: `GSI1PK` de `Expense` vira
   `USER#{userId}#{categoryId}`. Nenhum índice novo — é só uma mudança
   no valor gravado, não na estrutura da tabela. A checagem de exclusão
   bloqueada da FEAT-16 (`DynamoDbExpenseRepository.ExistsByCategoryAsync`,
   que já usa esse índice) não muda de implementação — só o valor que a
   `DeleteCategoryCommandHandler` passa pra ela (antes: `category.Nome`;
   agora: o próprio `categoryId` do comando, mais simples e exato).

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | `Expense.Category` (`ExpenseCategory`) → `Expense.CategoryId` (`string`); `ExpenseCategory.cs` **removido** |
| Application | `RegisterExpenseCommand`/`UpdateExpenseCommand`/`GetExpensesQuery` + Results + `ExpenseQueryFilter`/`ExpenseQueryItem`: campo `Category` → `CategoryId` (tipo `string`); Validators de `Register`/`UpdateExpense` ganham checagem assíncrona via `ICategoryRepository`; `GetExpensesQueryValidator` perde a regra de formato (categoria de filtro não precisa existir); `DeleteCategoryCommandHandler` (FEAT-16) passa a chamar `ExistsByCategoryAsync` com `categoryId` em vez de `category.Nome` |
| Infrastructure | `DynamoDbExpenseRepository`: `SaveAsync`/`UpdateAsync`/`GetByIdAsync`/`QueryAsync`/`MapToExpenseQueryItem` trocam o atributo `Category` (enum serializado) por `CategoryId` (string), e o `GSI1PK` passa a usar `categoryId` |
| Api | `ExpenseEndpoints`: `RegisterExpenseRequest`/`UpdateExpenseRequest`/`GetExpensesRequest` — campo `category`/`categoryId` (query) renomeados para `categoryId`; nenhuma rota nova, nenhum status code novo |
| AWS/Terraform | Nenhum recurso novo — reaproveita a tabela `GastosApp` e o `GSI1` já provisionados; só o **valor** gravado em `GSI1PK`/`GSI1SK`/atributo muda, não a estrutura |

## Domain-layer

`backend/src/GastosApp.Domain/Expenses/Expense.cs` (editar):

```csharp
public sealed class Expense
{
    public string Id { get; }
    public string UserId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public string CategoryId { get; }          // era ExpenseCategory Category
    public DateOnly ExpenseDate { get; }
    public DateTimeOffset CreatedAt { get; }

    // Create/Restore: mesma assinatura, só o parâmetro `category` (ExpenseCategory)
    // vira `categoryId` (string) — sem mais Enum.Parse em lugar nenhum do Domain/Application.
}
```

`backend/src/GastosApp.Domain/Expenses/ExpenseCategory.cs` — **excluído**.
Nenhum outro tipo do Domain referencia o enum.

## Application-layer

### `RegisterExpenseCommand` (editar)

```csharp
public sealed record RegisterExpenseCommand(
    string UserId, string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate)
    : ICommand<Result<RegisterExpenseResult>>;

// Handler: remove o `Enum.Parse<ExpenseCategory>` — usa command.CategoryId direto em Expense.Create.

public record RegisterExpenseResult(
    string Id, string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate, DateTimeOffset CreatedAt)
{
    public static RegisterExpenseResult FromExpense(Expense expense) => new(
        expense.Id, expense.Description, expense.AmountInCents, expense.CategoryId, expense.ExpenseDate, expense.CreatedAt);
}
```

### `RegisterExpenseCommandValidator` (editar — ganha `ICategoryRepository`)

```csharp
public sealed class RegisterExpenseCommandValidator : AbstractValidator<RegisterExpenseCommand>
{
    private const int MaxDescriptionLength = 200;
    private readonly ICategoryRepository _categoryRepository;

    public RegisterExpenseCommandValidator(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description).NotEmpty()... // inalterado
        RuleFor(c => c.AmountInCents).GreaterThan(0)... // inalterado

        RuleFor(c => c.CategoryId)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MustAsync(BeAnOwnedCategoryAsync).WithMessage("Categoria inválida.");
    }

    private async Task<bool> BeAnOwnedCategoryAsync(
        RegisterExpenseCommand command, string categoryId, CancellationToken ct) =>
        await _categoryRepository.GetByIdAsync(command.UserId, categoryId, ct) is not null;
}
```

`GetByIdAsync` (FEAT-16) já filtra por posse (`userId`) — categoria de
outro usuário retorna `null`, cai na mesma mensagem "Categoria
inválida." de uma categoria inexistente (US3/US5, sem vazar
existência). `UpdateExpenseCommandValidator` ganha exatamente a mesma
regra (mirror), recebendo `ICategoryRepository` também.

### `UpdateExpenseCommand`/`UpdateExpenseResult` — mesmo padrão de rename (`Category` → `CategoryId`, tipo `string`), Handler sem `Enum.Parse`.

### `GetExpensesQuery`/`ExpenseQueryFilter`/`ExpenseQueryItem`/`ExpenseSummary` (editar)

```csharp
public sealed record GetExpensesQuery(
    string UserId, string? YearMonth, string? CategoryId, string? DateFrom, string? DateTo,
    long? MinAmountInCents, long? MaxAmountInCents, string? Cursor, int? Limit) : IQuery<Result<GetExpensesResult>>;

public sealed record ExpenseQueryFilter(
    string UserId, string? YearMonth, string? CategoryId, DateOnly? DateFrom, DateOnly? DateTo,
    long? MinAmountInCents, long? MaxAmountInCents, string? Cursor, int Limit);

public sealed record ExpenseQueryItem(
    string Id, string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate, DateTimeOffset CreatedAt);

public sealed record ExpenseSummary(
    string Id, string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate, DateTimeOffset CreatedAt)
{
    public static ExpenseSummary FromQueryItem(ExpenseQueryItem item) => new(
        item.Id, item.Description, item.AmountInCents, item.CategoryId, item.ExpenseDate, item.CreatedAt);
}
```

`GetExpensesQueryHandler`: remove o `Enum.Parse<ExpenseCategory>` do
filtro — `CategoryId: query.CategoryId` direto, sem parsing.

### `GetExpensesQueryValidator` (editar — remove a regra de categoria)

```csharp
// Remove inteiramente:
// RuleFor(q => q.Category).Must(BeAValidCategory)...
// e o método BeAValidCategory.
```
Spec (US7) exige que um `categoryId` de filtro que não existe/sem
despesas retorne 200 com lista vazia, não 400 — não há mais "formato
válido" pra checar (era só o enum fechado), e a spec explicitamente diz
que não precisa existir. `categoryId` de filtro sem restrição alguma
(qualquer string, inclusive vazia via `NullIfEmpty` já existente no
endpoint).

### `DeleteCategoryCommandHandler` (FEAT-16, editar — simplifica)

```csharp
public async ValueTask<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
{
    var category = await _categoryRepository.GetByIdAsync(command.UserId, command.CategoryId, cancellationToken);
    if (category is null)
        return Result.Failure(CategoryErrors.NotFound);

    var inUse = await _expenseRepository.ExistsByCategoryAsync(command.UserId, command.CategoryId, cancellationToken);
    // era category.Nome — agora usa o próprio categoryId do comando (idêntico a category.Id,
    // não precisa mais do objeto Category pra isso, só continua buscando pra confirmar existência/posse)
    if (inUse)
        return Result.Failure(CategoryErrors.CategoryInUse);

    var deleted = await _categoryRepository.DeleteAsync(command.UserId, command.CategoryId, cancellationToken);
    return deleted ? Result.Success() : Result.Failure(CategoryErrors.NotFound);
}
```
`IExpenseRepository.ExistsByCategoryAsync` **não muda de assinatura**
(já era `string category` — continua `string`, só passa a receber um
`categoryId` em vez de um nome de enum).

### `IExpenseRepository` (editar — só tipos, não assinatura)

```csharp
public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
    Task<Expense?> GetByIdAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string userId, string categoryId, CancellationToken cancellationToken = default); // rename do parâmetro
    Task<Expense?> UpdateAsync(
        string userId, string expenseId, string description, long amountInCents,
        string categoryId, DateOnly expenseDate, CancellationToken cancellationToken = default); // era ExpenseCategory category
}
```

## Infrastructure-layer — `DynamoDbExpenseRepository`

Item model (só o que muda):

| Atributo | Antes | Depois |
|---|---|---|
| `GSI1PK` | `USER#{userId}#{ExpenseCategory}` (ex.: `USER#u1#Alimentacao`) | `USER#{userId}#{categoryId}` (ex.: `USER#u1#7f3e9a10-...`) |
| Atributo de categoria | `Category` (`S`, nome do enum) | `CategoryId` (`S`, id opaco) |

`SaveAsync`/`UpdateAsync`: trocar
`["Category"] = new AttributeValue { S = expense.Category.ToString() }`
por `["CategoryId"] = new AttributeValue { S = expense.CategoryId }`, e
`GSI1PK`/`GSI1SK` compostos com `expense.CategoryId` em vez de
`expense.Category`. `MapToExpenseQueryItem`/`GetByIdAsync`: trocar
`Category: Enum.Parse<ExpenseCategory>(item["Category"].S)` por
`CategoryId: item["CategoryId"].S` (leitura direta, sem parsing).
`BuildQueryRequest`: `filter.Category is not null` vira
`filter.CategoryId is not null` pra decidir `GSI1` vs tabela base —```
mesma lógica, só o nome do campo.

`ExistsByCategoryAsync` (FEAT-16) **não muda uma linha** — já monta
`GSI1PK = USER#{userId}#{category}` com o parâmetro recebido; o
`DeleteCategoryCommandHandler` é quem passa um valor diferente agora
(`categoryId` em vez de nome).

## Api-layer

`RegisterExpenseRequest`/`UpdateExpenseRequest`:
```csharp
public record RegisterExpenseRequest(string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate);
public record UpdateExpenseRequest(string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate);
```

`GetExpensesRequest`: `Category` → `CategoryId` (query string, mesmo
padrão de `NullIfEmpty`).

Handlers dos endpoints: só trocam `request.Category`/`request.CategoryId`
por `request.CategoryId` ao montar `Command`/`Query` — nenhuma mudança
de rota, verbo ou status code.

`AppJsonSerializerContext`: nenhum `[JsonSerializable]` novo (mesmos
tipos `RegisterExpenseRequest`/`UpdateExpenseRequest`/
`RegisterExpenseResult`/`UpdateExpenseResult`/`GetExpensesResult`/
`ExpenseSummary`/`GetExpensesRequest` já registrados — só os campos
internos mudam de tipo, o gerador de source code não precisa de
alteração de atributo).

## Mapeamento de erros

| Cenário | `ErrorType` | Status HTTP | `type` (slug) |
|---|---|---|---|
| `categoryId` ausente/vazio | `Validation` | 400 | `validation-error` |
| `categoryId` inexistente | `Validation` | 400 | `validation-error` (reaproveita a mesma mensagem "Categoria inválida.", sem `code` novo) |
| `categoryId` de categoria de outro usuário | `Validation` | 400 | `validation-error` (mesmo tratamento acima — `GetByIdAsync` já filtra por posse, não diferencia dos dois casos) |
| Demais validações de despesa (descrição, valor) | `Validation` | 400 | `validation-error` (inalterado da FEAT-04) |
| Despesa inexistente/de outro usuário (`GET`/`PUT`/`DELETE /expenses/{id}`) | `NotFound` | 404 | `not-found` (inalterado da FEAT-06/07/08) |
| Sem token | — (middleware) | 401 | `unauthorized` (inalterado) |
| `DELETE /categories/{id}` com despesa vinculada | `UnprocessableEntity` | 422 | `category-in-use` (comportamento da FEAT-16 preservado, só a checagem interna muda de nome pra id) |

Nenhum `Error`/`ErrorType` novo nesta feature — reaproveita
`ErrorType.Validation` já existente (mesmo código `validation-error`
usado por toda validação de campo desde a FEAT-04/`ValidationBehavior`).

## Recursos AWS

Nenhum recurso novo. Reaproveita a tabela `GastosApp` e o `GSI1` já
provisionados — só o **conteúdo** gravado em `GSI1PK`/atributo de
categoria muda de formato (nome do enum → `categoryId`). Como o usuário
vai apagar manualmente os dados existentes antes de subir esta feature
(decisão já registrada em `spec.md`), não há necessidade de nenhuma
migração/backfill nem em `hom` nem em `prod`.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`) — editar/reescrever

- `Domain/ExpenseTests.cs` — trocar `ExpenseCategory.Alimentacao` por
  um `categoryId` de exemplo (string) em todos os casos
- `Application/RegisterExpenseCommandValidatorTests.cs` — trocar o
  teste "categoria fora do enum" por "categoria inexistente"/"de outro
  usuário" (mock de `ICategoryRepository.GetByIdAsync` retornando
  `null`); novo teste "categoria existente e própria" válida (mock
  retornando uma `Category`)
- `Application/UpdateExpenseCommandValidatorTests.cs` — mesmo mirror
- `Application/RegisterExpenseCommandHandlerTests.cs`/
  `UpdateExpenseCommandHandlerTests.cs` — ajustar asserts de
  `result.Value.Category` para `CategoryId`
- `Application/GetExpensesQueryHandlerTests.cs` — filtro por
  `CategoryId` (string) em vez de `ExpenseCategory`; remover teste que
  dependia de enum inválido
- `Application/GetExpensesQueryValidatorTests.cs` — remover casos de
  "categoria fora do enum" (não existe mais validação de formato)
- `Application/GetExpenseByIdQueryHandlerTests.cs` — ajustar campo
- `Application/DeleteCategoryCommandHandlerTests.cs` (FEAT-16) —
  `ExistsByCategoryAsync` agora é chamado com `command.CategoryId`
  (não mais `category.Nome`) — ajustar `Arg.Is`
- `Infrastructure/DynamoDbExpenseRepository{Delete,GetById,Update,Query}Tests.cs`
  — trocar fixtures de `Category`/`ExpenseCategory` por `CategoryId`
  (string), `GSI1PK` esperado passa a usar o valor de `categoryId`
- `Infrastructure/DynamoDbExpenseRepositoryExistsByCategoryTests.cs` —
  sem mudança estrutural, só renomear variável de teste pra refletir
  que agora é um id, não um nome

### Component tests (`backend/tests/GastosApp.ComponentTests/`) — editar/reescrever

- `Expenses/ExpenseEndpointsTests.cs`: todo payload de request/response
  troca `category: "Alimentacao"` por `categoryId: "<id>"`; toda
  criação/atualização bem-sucedida precisa mockar
  `CategoryRepositoryMock.GetByIdAsync(userId, categoryId, ...)`
  retornando uma `Category` válida (senão a validação assíncrona falha
  com 400); casos de categoria inválida mockam retorno `null`
- `Categories/CategoryEndpointsTests.cs` (FEAT-16): teste
  `DeleteCategory_ComDespesasAssociadas_Retorna422SemExcluir` passa a
  mockar `ExpenseRepositoryMock.ExistsByCategoryAsync(userId,
  categoryId, ...)` (o `categoryId` da própria categoria sendo
  excluída) em vez de um nome como `"Alimentacao"`

## Critical Files

- `backend/src/GastosApp.Domain/Expenses/Expense.cs` — `CategoryId`
- `backend/src/GastosApp.Domain/Expenses/ExpenseCategory.cs` — excluído
- `backend/src/GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs`
- `backend/src/GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommandValidator.cs`
- `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommand.cs`
- `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommandValidator.cs`
- `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs`
- `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQueryValidator.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/ExpenseQueryFilter.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/ExpenseQueryItem.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs`
- `backend/src/GastosApp.Application/Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs`
- `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs`
- `backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln` — confirma que a remoção de
  `ExpenseCategory` não deixa referência solta em nenhuma camada
- `dotnet test backend/GastosApp.sln` — suíte completa (Unit +
  Component), sem regressão em `Categories`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (mudança de contrato: campo `category`→`categoryId` em 4 endpoints)
- Smoke manual (opcional, contra AWS real, depois do usuário apagar os
  dados antigos): criar categoria, registrar despesa com o
  `categoryId` dela, confirmar `GET /expenses` retornando o
  `categoryId`, renomear a categoria e confirmar que a despesa
  continua íntegra, tentar excluir a categoria (espera 422), excluir a
  despesa e tentar de novo (espera 204)

## Pontos que precisam de confirmação antes do `/tasks`

1. **Nome do atributo no DynamoDB**: proponho renomear o atributo de
   `Category` para `CategoryId` no item (não só mudar o valor) — mais
   claro pra quem for inspecionar a tabela depois, e sem custo (a base
   vai ser apagada mesmo). Confirmar que não há preferência por manter
   o nome do atributo `Category` (só trocando o que ele contém).
2. **Mensagem de erro genérica "Categoria inválida."**: mantida
   idêntica à usada desde a FEAT-04 pro enum fechado, agora reaproveitada
   pro caso de `categoryId` inexistente/de outro usuário — confirmar que
   não é necessário uma mensagem mais específica (ex.: diferenciar
   "categoria não encontrada" de "formato inválido"), já que a spec
   pede explicitamente não diferenciar categoria inexistente de
   categoria de outro usuário, e generalizar pra "campo ausente" também
   simplifica.
3. **`GetExpensesQueryValidator` fica sem nenhuma regra pra
   `CategoryId`**: confirmar que realmente não deve haver nenhuma
   validação de formato no filtro (ex.: rejeitar strings claramente
   inválidas como muito longas) — a spec (US7) só exige que um id sem
   correspondência retorne lista vazia, não que exista validação de
   formato alguma.
