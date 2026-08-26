# Plan: FEAT-22 — Transações: generalizar Despesa para Receita/Despesa — Plano Técnico

## Contexto técnico

`spec.md` fecha: rota única `/transactions` (não `/expenses`+`/incomes`),
substituindo `/expenses` por completo; novo campo `tipo`
(`despesa`|`receita`, obrigatório, validado contra o `tipo` da
`Category` referenciada); `expenseDate` renomeado para `date`;
`createdByUserId`/`createdByLabel` expostos (autor nunca muda);
`Lancar` passa a poder `PUT`/`DELETE` só o que criou.

Isto é essencialmente um **rename em cascata** de `Expense` para
`Transaction` nas quatro camadas (ver seção "Renomeação"), mais três
decisões técnicas não óbvias a partir do `spec.md`:

1. **O atributo `Tipo` do item DynamoDB já existe e já é reaproveitável
   como o novo campo de negócio — ao contrário do que a FEAT-21
   precisou fazer para `Category`.** Hoje `DynamoDbExpenseRepository`
   grava `Tipo="despesa"` como constante — não é um campo de negócio,
   é o **discriminador do `GSI2` compartilhado** entre `Category`
   (`Tipo="categoria"`) e `Expense` (`Tipo="despesa"`), usado por
   `IsDespesaItem` pra impedir que um `id` de categoria seja lido/
   apagado por engano pelas rotas de despesa (ver
   `backend/docs/data-model.md`, "Espaço de chave compartilhado").
   A FEAT-21 precisou criar um atributo **novo** (`TipoLancamento`)
   pra `Category` porque o discriminador dela (`Tipo="categoria"`) é
   fixo — não pode carregar um valor de negócio variável sem quebrar o
   discriminador. Para `Transaction` a situação é o oposto: o
   discriminador já é o campo que queremos tornar variável, e os dois
   valores de negócio (`"despesa"`/`"receita"`) continuam,
   coincidentemente, sendo válidos como discriminador (nenhum dos dois
   é `"categoria"`). **Não é preciso nenhum atributo novo** — só
   passar a gravar `command.Tipo` em vez da constante `"despesa"`, e
   generalizar a checagem de `IsDespesaItem` (`tipo.S == "despesa"`)
   para `IsTransactionItem` (`tipo.S != "categoria"`, aceita os dois
   valores sem precisar listá-los).
2. **Autorização "papel `Lancar` só edita/exclui o que criou" não cabe
   no `RoleEndpointFilters` atual.** O filtro hoje (`Require(params
   MembershipRole[])`) é estático por rota — não tem acesso ao recurso
   sendo afetado, só ao papel do chamador. `PUT`/`DELETE
   /transactions/{id}` passam a permitir `Lancar` **no gate estático**
   (junto com `Total`/`Titular`), e a checagem de posse
   (`createdByUserId` da transação == chamador) é feita dentro do
   `Handler` do Command — que já precisa buscar a transação existente
   pra decidir 404 vs. seguir, então a checagem de posse é só mais um
   `if` nesse mesmo ponto, sem filtro novo nem infraestrutura de
   autorização adicional. `CurrentAccountContext` ganha `UserId` (novo
   campo, populado por `ResolveAccountEndpointFilter` a partir do mesmo
   `userId` que ele já extrai do JWT hoje — só passa a guardá-lo, sem
   mudar de onde vem).
3. **Confirmado com o usuário: a tabela `GastosApp` é recriada do zero
   antes do deploy desta feature (todos os tipos de item, não só
   transações)** — ao contrário do que a FEAT-21 encontrou na prática
   (tabela real de hom/prod nunca foi recriada apesar do roadmap
   permitir), aqui o usuário confirmou explicitamente que os dados
   atuais são só de teste e podem ser descartados. Isso elimina por
   completo a necessidade de tratar `CreatedByUserId` (ou qualquer
   outro atributo novo) como "possivelmente ausente" — `Transaction`
   não precisa de nenhum caso legado, `CreatedByUserId` é sempre
   presente. Ver "Recursos AWS" para o runbook de recriação e decisão
   técnica 3.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | `Expense` → `Transaction`: ganha `Tipo` (`string`) e `CreatedByUserId` (`string`, sempre presente); `ExpenseDate` renomeado para `Date` |
| Application | Todo o módulo `Expenses/*` renomeado para `Transactions/*` (Commands/Queries/Validators/Results); `ITransactionRepository` ganha `Tipo` nos parâmetros de escrita/filtro; `UpdateTransactionCommand`/`DeleteTransactionCommand` ganham `CallerUserId`/`CallerRole` pra checagem de posse; `GetTransactionsQuery`/`GetTransactionByIdQuery` ganham `CallerUserId` pra resolver `createdByLabel`; `DeleteCategoryCommandHandler` passa a depender de `ITransactionRepository` |
| Infrastructure | `DynamoDbExpenseRepository` → `DynamoDbTransactionRepository`: `Tipo` deixa de ser constante, novo atributo `CreatedByUserId` (sempre presente — tabela recriada), filtro de `tipo` em `QueryAsync`, `ConditionExpression` do `DeleteAsync` generalizada, atributo de data renomeado de `ExpenseDate` para `Date` |
| Api | `ExpenseEndpoints` → `TransactionEndpoints`, rota `/expenses` → `/transactions`; `CurrentAccountContext` ganha `UserId`; `ResolveAccountEndpointFilter` popula esse campo; matriz de papéis de `PUT`/`DELETE` ganha `Lancar` no gate estático |
| AWS/Terraform | Nenhum recurso novo — mesma tabela `GastosApp`, mesmos `GSI1`/`GSI2` já provisionados; `CreatedByUserId` é atributo regular (não indexado) |

## Renomeação (mapa completo, sem mudança de comportamento além do listado nas seções seguintes)

| Camada | Antes | Depois |
|---|---|---|
| Domain | `GastosApp.Domain/Expenses/Expense.cs` | `GastosApp.Domain/Transactions/Transaction.cs` |
| Application (interface) | `Common/Interfaces/IExpenseRepository.cs` | `Common/Interfaces/ITransactionRepository.cs` |
| Application (interface) | `Common/Interfaces/ExpenseQueryFilter.cs` | `Common/Interfaces/TransactionQueryFilter.cs` |
| Application (interface) | `Common/Interfaces/ExpenseQueryItem.cs` | `Common/Interfaces/TransactionQueryItem.cs` |
| Application (interface) | `Common/Interfaces/ExpenseQueryPage.cs` | `Common/Interfaces/TransactionQueryPage.cs` |
| Application (cursor) | `Common/Cursors/ExpenseCursorCodec.cs` | `Common/Cursors/TransactionCursorCodec.cs` |
| Application (cursor) | `Common/Cursors/ExpenseCursorPayload.cs` | `Common/Cursors/TransactionCursorPayload.cs` |
| Application (cursor) | `Common/Cursors/ExpenseCursorJsonContext.cs` | `Common/Cursors/TransactionCursorJsonContext.cs` |
| Application (commands) | `Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs` | `Transactions/Commands/RegisterTransaction/RegisterTransactionCommand.cs` |
| Application (commands) | `Expenses/Commands/UpdateExpense/UpdateExpenseCommand(Validator).cs` | `Transactions/Commands/UpdateTransaction/UpdateTransactionCommand(Validator).cs` |
| Application (commands) | `Expenses/Commands/DeleteExpense/DeleteExpenseCommand.cs` | `Transactions/Commands/DeleteTransaction/DeleteTransactionCommand.cs` |
| Application (queries) | `Expenses/Queries/GetExpenses/GetExpensesQuery(Validator).cs` | `Transactions/Queries/GetTransactions/GetTransactionsQuery(Validator).cs` |
| Application (queries) | `Expenses/Queries/GetExpenseById/GetExpenseByIdQuery.cs` | `Transactions/Queries/GetTransactionById/GetTransactionByIdQuery.cs` |
| Application (erros) | `Expenses/ExpenseErrors.cs` | `Transactions/TransactionErrors.cs` |
| Infrastructure | `Expenses/DynamoDbExpenseRepository.cs` | `Transactions/DynamoDbTransactionRepository.cs` |
| Api | `Endpoints/ExpenseEndpoints.cs` (grupo `/expenses`) | `Endpoints/TransactionEndpoints.cs` (grupo `/transactions`) |

`Program.cs`: `app.MapExpenseEndpoints()` → `app.MapTransactionEndpoints()`.

Testes seguem o mesmo mapa (ver "Plano de testes").

## Domain-layer

`backend/src/GastosApp.Domain/Transactions/Transaction.cs`:

```csharp
public sealed class Transaction
{
    public string Id { get; }
    public string AccountId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public string CategoryId { get; }
    public string Tipo { get; }                 // "despesa" | "receita" — sem enum, mesmo padrão de Category.Tipo (FEAT-21)
    public DateOnly Date { get; }                // "ExpenseDate" no Domain vira "Date" — atributo DynamoDB também renomeado (tabela recriada, sem custo de compatibilidade — ver decisão técnica 3)
    public string CreatedByUserId { get; }       // sempre presente — tabela recriada antes do deploy desta feature, sem caso legado (ver Contexto técnico, ponto 3)
    public DateTimeOffset CreatedAt { get; }

    private Transaction(
        string id, string accountId, string description, long amountInCents,
        string categoryId, string tipo, DateOnly date, string createdByUserId, DateTimeOffset createdAt)
    {
        Id = id; AccountId = accountId; Description = description; AmountInCents = amountInCents;
        CategoryId = categoryId; Tipo = tipo; Date = date; CreatedByUserId = createdByUserId; CreatedAt = createdAt;
    }

    public static Transaction Create(
        string accountId, string description, long amountInCents,
        string categoryId, string tipo, DateOnly date, string createdByUserId) =>
        new(Guid.NewGuid().ToString(), accountId, description, amountInCents,
            categoryId, tipo, date, createdByUserId, DateTimeOffset.UtcNow);

    public static Transaction Restore(
        string id, string accountId, string description, long amountInCents,
        string categoryId, string tipo, DateOnly date, string createdByUserId, DateTimeOffset createdAt) =>
        new(id, accountId, description, amountInCents, categoryId, tipo, date, createdByUserId, createdAt);
}
```

Sem `enum TransactionTipo`, mesma decisão e mesmo raciocínio já
registrados no `plan.md` da FEAT-21 (ponto 4 de "Pontos a confirmar"
de lá, resolvido aqui): `Tipo` seria só um atributo de dado, validado
no Validator — não há lógica condicional por tipo no Domain nesta
feature (a validação cruzada com `Category.Tipo` mora no Validator,
que já depende de `ICategoryRepository`).

## Application-layer

### `ITransactionRepository` (`Common/Interfaces/ITransactionRepository.cs`)

```csharp
public interface ITransactionRepository
{
    Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<TransactionQueryPage> QueryAsync(TransactionQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
    Task<Transaction?> UpdateAsync(
        string accountId, string transactionId, string description, long amountInCents,
        string categoryId, string tipo, DateOnly date, CancellationToken cancellationToken = default);
}
```
`UpdateAsync` **não** recebe `createdByUserId` — o autor nunca muda
numa edição (regra de negócio da spec), então não há parâmetro pra
sobrescrevê-lo; a implementação preserva o valor já gravado no item,
mesmo mecanismo que já preserva `CreatedAt` hoje.

```csharp
public sealed record TransactionQueryFilter(
    string AccountId, string? Tipo, string? YearMonth, string? CategoryId,
    DateOnly? DateFrom, DateOnly? DateTo, long? MinAmountInCents, long? MaxAmountInCents,
    string? Cursor, int Limit);

public sealed record TransactionQueryItem(
    string Id, string Description, long AmountInCents, string CategoryId, string Tipo,
    DateOnly Date, string CreatedByUserId, DateTimeOffset CreatedAt);

public sealed record TransactionQueryPage(IReadOnlyList<TransactionQueryItem> Items, string? NextCursor);
```

### `RegisterTransactionCommand` (`Transactions/Commands/RegisterTransaction/RegisterTransactionCommand.cs`)

```csharp
public sealed record RegisterTransactionCommand(
    string AccountId, string Description, long AmountInCents, string CategoryId,
    string Tipo, DateOnly Date, string CreatedByUserId) : ICommand<Result<RegisterTransactionResult>>;

public sealed class RegisterTransactionCommandHandler : ICommandHandler<RegisterTransactionCommand, Result<RegisterTransactionResult>>
{
    public async ValueTask<Result<RegisterTransactionResult>> Handle(RegisterTransactionCommand command, CancellationToken ct)
    {
        var transaction = Transaction.Create(
            command.AccountId, command.Description, command.AmountInCents,
            command.CategoryId, command.Tipo, command.Date, command.CreatedByUserId);

        await _transactionRepository.SaveAsync(transaction, ct);

        // Quem cria é sempre o próprio chamador — "Você" sem precisar consultar Membership aqui
        // (diferente de GetTransactions/GetTransactionById, que podem mostrar autoria de outro membro).
        return Result.Success(RegisterTransactionResult.FromEntity(transaction, createdByLabel: "Você"));
    }
}

public sealed record RegisterTransactionResult(
    string Id, string Description, long AmountInCents, string CategoryId, string Tipo,
    DateOnly Date, string CreatedByUserId, string CreatedByLabel, DateTimeOffset CreatedAt)
{
    public static RegisterTransactionResult FromEntity(Transaction t, string createdByLabel) => new(
        t.Id, t.Description, t.AmountInCents, t.CategoryId, t.Tipo, t.Date,
        t.CreatedByUserId, createdByLabel, t.CreatedAt);
}
```

### `RegisterTransactionCommandValidator` — cruzamento com `Category.Tipo`

```csharp
public sealed class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    private const int MaxDescriptionLength = 200;
    private readonly ICategoryRepository _categoryRepository;

    public RegisterTransactionCommandValidator(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description).NotEmpty()...MaximumLength(MaxDescriptionLength)...; // inalterado (FEAT-04)
        RuleFor(c => c.AmountInCents).GreaterThan(0)...; // inalterado (FEAT-04)

        RuleFor(c => c.Tipo)
            .NotEmpty().WithMessage("Tipo é obrigatório.")
            .Must(t => t is "despesa" or "receita").WithMessage("Tipo deve ser \"despesa\" ou \"receita\".");

        RuleFor(c => c.CategoryId)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MustAsync(BeAnOwnedCategoryOfMatchingTypeAsync).WithMessage("Categoria inválida.");
    }

    // Uma só chamada ao repositório cobre os três casos que a spec trata como
    // indistinguíveis (inexistente / de outra conta / tipo divergente) — mesmo
    // padrão de "não vazar detalhe" já usado pela FEAT-17 pra categoria de outro usuário.
    private async Task<bool> BeAnOwnedCategoryOfMatchingTypeAsync(
        RegisterTransactionCommand command, string categoryId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(command.AccountId, categoryId, ct);
        return category is not null && category.Tipo == command.Tipo;
    }
}
```
`UpdateTransactionCommandValidator` é o mesmo código, mirrorando
`UpdateExpenseCommandValidator` hoje (`CategoryId` da command em vez de
`command.CategoryId` — já é assim).

### `UpdateTransactionCommand` — posse por `Lancar`

```csharp
public sealed record UpdateTransactionCommand(
    string AccountId, string TransactionId, string CallerUserId, MembershipRole CallerRole,
    string Description, long AmountInCents, string CategoryId, string Tipo, DateOnly Date)
    : ICommand<Result<UpdateTransactionResult>>;

public sealed class UpdateTransactionCommandHandler : ICommandHandler<UpdateTransactionCommand, Result<UpdateTransactionResult>>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMembershipRepository _membershipRepository;

    public async ValueTask<Result<UpdateTransactionResult>> Handle(UpdateTransactionCommand command, CancellationToken ct)
    {
        var existing = await _transactionRepository.GetByIdAsync(command.AccountId, command.TransactionId, ct);
        if (existing is null)
            return Result.Failure<UpdateTransactionResult>(TransactionErrors.NotFound);

        if (command.CallerRole == MembershipRole.Lancar && existing.CreatedByUserId != command.CallerUserId)
            return Result.Failure<UpdateTransactionResult>(MembershipErrors.InsufficientPermission);

        var updated = await _transactionRepository.UpdateAsync(
            command.AccountId, command.TransactionId, command.Description, command.AmountInCents,
            command.CategoryId, command.Tipo, command.Date, ct);

        if (updated is null) // defensivo: GetByIdAsync acima já confirmou existência
            return Result.Failure<UpdateTransactionResult>(TransactionErrors.NotFound);

        var label = await CreatedByLabelResolver.ResolveAsync(
            _membershipRepository, command.AccountId, updated.CreatedByUserId, command.CallerUserId, ct);
        return Result.Success(UpdateTransactionResult.FromEntity(updated, label));
    }
}
```
Reutiliza `MembershipErrors.InsufficientPermission` (mesmo `Error` já
usado por `RoleEndpointFilters`) em vez de duplicar um erro equivalente
em `TransactionErrors` — mesmo código/mensagem/tipo (403), fonte
única.

`GetByIdAsync` roda antes de `UpdateAsync`/`DeleteAsync`, que fazem seu
próprio lookup interno (`GSI2` + `GetItem`) de novo — duas leituras em
vez de uma. Aceito deliberadamente (ver decisão técnica 2): é o único
jeito de checar posse antes de escrever sem duplicar a lógica de
delete/update do repositório, e o custo de uma leitura extra em
DynamoDB on-demand é desprezível no volume de uso pessoal do projeto
(mesmo raciocínio de custo já usado em várias specs anteriores).

`DeleteTransactionCommand` segue o mesmo formato (`CallerUserId`/
`CallerRole`, `GetByIdAsync` antes de `DeleteAsync`, sem `UpdateResult`
— só `Result`).

### `CreatedByLabelResolver` (novo, `Transactions/Common/CreatedByLabelResolver.cs`)

Helper estático reaproveitado por `RegisterTransactionCommandHandler`
(caso trivial, sempre `"Você"`, sem chamar isto), `UpdateTransactionCommandHandler`,
`GetTransactionByIdQueryHandler` e `GetTransactionsQueryHandler`:

```csharp
internal static class CreatedByLabelResolver
{
    public static async Task<string> ResolveAsync(
        IMembershipRepository membershipRepository, string accountId,
        string createdByUserId, string callerUserId, CancellationToken cancellationToken)
    {
        if (createdByUserId == callerUserId)
            return "Você";

        var membership = await membershipRepository.FindByAccountAndUserIdAsync(accountId, createdByUserId, cancellationToken);
        // Hoje (FEAT-20) DELETE /members ainda apaga o Membership de fato, mesmo
        // que o membro tenha transações lançadas — "Ex-membro" cobre esse caso.
        // Confirmado com o usuário como débito técnico (ver backend/docs/roadmap.md):
        // um membro com transações deveria virar Inativo em vez de removido; quando
        // isso for implementado, o Membership nunca mais desaparece de fato (só o
        // Status muda), então este fallback deixa de disparar — sem exigir nenhuma
        // mudança neste resolver, já que FindByAccountAndUserIdAsync não filtra por
        // Status hoje.
        return membership?.Email ?? "Ex-membro";
    }
}
```

### `GetTransactionsQuery`/`GetTransactionByIdQuery` — `CallerUserId` + cache de label por página

```csharp
public sealed record GetTransactionsQuery(
    string AccountId, string CallerUserId, string? Tipo, string? YearMonth, string? CategoryId,
    string? DateFrom, string? DateTo, long? MinAmountInCents, long? MaxAmountInCents,
    string? Cursor, int? Limit) : IQuery<Result<GetTransactionsResult>>;

public sealed class GetTransactionsQueryHandler : IQueryHandler<GetTransactionsQuery, Result<GetTransactionsResult>>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMembershipRepository _membershipRepository;

    public async ValueTask<Result<GetTransactionsResult>> Handle(GetTransactionsQuery query, CancellationToken ct)
    {
        var filter = new TransactionQueryFilter(
            query.AccountId, query.Tipo, query.YearMonth, query.CategoryId,
            ParseDate(query.DateFrom), ParseDate(query.DateTo),
            query.MinAmountInCents, query.MaxAmountInCents, query.Cursor, query.Limit ?? DefaultLimit);

        var page = await _transactionRepository.QueryAsync(filter, ct);

        // Cache por página — evita repetir FindByAccountAndUserIdAsync pro mesmo
        // createdByUserId em toda transação da lista (caso comum: um segundo
        // membro lançando várias despesas seguidas).
        var labelCache = new Dictionary<string, string>();
        var items = new List<TransactionSummary>(page.Items.Count);
        foreach (var item in page.Items)
        {
            if (!labelCache.TryGetValue(item.CreatedByUserId, out var label))
            {
                label = await CreatedByLabelResolver.ResolveAsync(
                    _membershipRepository, query.AccountId, item.CreatedByUserId, query.CallerUserId, ct);
                labelCache[item.CreatedByUserId] = label;
            }
            items.Add(TransactionSummary.FromQueryItem(item, label));
        }

        return Result.Success(new GetTransactionsResult(items, page.NextCursor));
    }
}

public sealed record TransactionSummary(
    string Id, string Description, long AmountInCents, string CategoryId, string Tipo,
    DateOnly Date, string CreatedByUserId, string CreatedByLabel, DateTimeOffset CreatedAt)
{
    public static TransactionSummary FromQueryItem(TransactionQueryItem item, string createdByLabel) => new(
        item.Id, item.Description, item.AmountInCents, item.CategoryId, item.Tipo,
        item.Date, item.CreatedByUserId, createdByLabel, item.CreatedAt);
}
```
`GetTransactionByIdQueryHandler` é a mesma lógica sem o cache (só um
item): busca a transação, chama `CreatedByLabelResolver.ResolveAsync`
uma vez, monta o result (mesmo shape de `UpdateTransactionResult`,
seguindo o padrão já usado por `GetExpenseByIdQuery` que reaproveita
`UpdateExpenseResult` hoje).

### `GetTransactionsQueryValidator` — mirror de `GetExpensesQueryValidator` + regra de `tipo`

Mesmas regras já existentes (`yearMonth`, `dateFrom`/`dateTo`,
`minAmountInCents`/`maxAmountInCents`, `limit`, `cursor` — usando
`TransactionCursorCodec.TryDecode`), mais:

```csharp
RuleFor(q => q.Tipo)
    .Must(tipo => tipo is null or "despesa" or "receita")
    .WithMessage("tipo deve ser \"despesa\" ou \"receita\".");
```

### `TransactionErrors` (`Transactions/TransactionErrors.cs`)

```csharp
public static class TransactionErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Transação não encontrada.");
}
```
Sem `Error` de permissão próprio — reaproveita
`MembershipErrors.InsufficientPermission` (ver acima).

### `DeleteCategoryCommandHandler` — troca de dependência

```csharp
public sealed class DeleteCategoryCommandHandler : ICommandHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository; // era IExpenseRepository

    public async ValueTask<Result> Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(command.AccountId, command.CategoryId, ct);
        if (category is null) return Result.Failure(CategoryErrors.NotFound);

        var inUse = await _transactionRepository.ExistsByCategoryAsync(command.AccountId, command.CategoryId, ct);
        if (inUse) return Result.Failure(CategoryErrors.CategoryInUse);

        var deleted = await _categoryRepository.DeleteAsync(command.AccountId, command.CategoryId, ct);
        return deleted ? Result.Success() : Result.Failure(CategoryErrors.NotFound);
    }
}
```
`CategoryErrors.CategoryInUse` (`Categories/CategoryErrors.cs`): só o
texto da mensagem muda, de "despesas" pra "transações" — código
(`category-in-use`) e `ErrorType` (`UnprocessableEntity`, 422)
inalterados:
```csharp
public static Error CategoryInUse => Error.UnprocessableEntity(
    "category-in-use", "A categoria não pode ser excluída enquanto houver transações associadas a ela.");
```

### `ApplicationServiceCollectionExtensions.AddApplicationServices` — registros

```csharp
services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
services.AddScoped<IValidator<UpdateCategoryCommand>, UpdateCategoryCommandValidator>();
services.AddScoped<IValidator<GetCategoriesQuery>, GetCategoriesQueryValidator>();
services.AddScoped<IValidator<RegisterTransactionCommand>, RegisterTransactionCommandValidator>(); // era RegisterExpenseCommand
services.AddScoped<IValidator<UpdateTransactionCommand>, UpdateTransactionCommandValidator>();       // era UpdateExpenseCommand
services.AddScoped<IValidator<GetTransactionsQuery>, GetTransactionsQueryValidator>();               // era GetExpensesQuery
services.AddScoped<IValidator<InviteMemberCommand>, InviteMemberCommandValidator>();
services.AddScoped<IValidator<UpdateMemberRoleCommand>, UpdateMemberRoleCommandValidator>();
```

## Infrastructure-layer — `DynamoDbTransactionRepository`

Item model (atualizado):

| Atributo | Valor |
|---|---|
| `PK` | `ACCOUNT#{accountId}` (inalterado) |
| `SK` | `TXN#{date}#{id}` (inalterado — reaproveita a mecânica de chave já existente, conforme roadmap) |
| `GSI1PK` | `ACCOUNT#{accountId}#{categoryId}` (inalterado) |
| `GSI1SK` | `{date}#{id}` (inalterado) |
| `GSI2PK` | `ID#{id}` (inalterado) |
| `Description`, `AmountInCents`, `CategoryId` | inalterados |
| `Date` | **renomeado de `ExpenseDate`** — sem custo de compatibilidade porque a tabela é recriada do zero antes do deploy (ver Contexto técnico, ponto 3, e decisão técnica 3) |
| `Tipo` | **deixa de ser a constante `"despesa"`** — grava `transaction.Tipo` (`"despesa"`\|`"receita"`). Continua servindo de discriminador do `GSI2` compartilhado com `Category` (ver Contexto técnico, ponto 1) |
| `CreatedByUserId` | **novo**, atributo `S`, sempre presente (gravado em toda transação por `SaveAsync`, tabela sem item legado a considerar) |
| `CreatedAt` | inalterado |

```csharp
private const string TipoAttribute = "Tipo";
private const string TipoCategoria = "categoria"; // valor gravado por Category — único valor que NÃO é uma Transaction

// Generalização de "IsDespesaItem": aceita "despesa" e "receita" sem listar as
// duas — qualquer Tipo diferente de "categoria" já é suficiente pra discriminar
// uma Transaction de uma Category no GSI2 compartilhado.
private static bool IsTransactionItem(Dictionary<string, AttributeValue> item) =>
    item.TryGetValue(TipoAttribute, out var tipo) && tipo.S != TipoCategoria;
```

`SaveAsync`:
```csharp
["Date"] = new AttributeValue { S = transaction.Date.ToString(DateFormat) }, // era "ExpenseDate"
["Tipo"] = new AttributeValue { S = transaction.Tipo },                      // era constante "despesa"
["CreatedByUserId"] = new AttributeValue { S = transaction.CreatedByUserId },
```

`DeleteAsync` — `ConditionExpression` generalizada (mesma proteção de
hoje, agora cobrindo os dois tipos sem enumerá-los):
```csharp
ConditionExpression = "attribute_exists(PK) AND #tipo <> :tipoCategoria",
ExpressionAttributeNames = new() { ["#tipo"] = TipoAttribute },
ExpressionAttributeValues = new() { [":tipoCategoria"] = new AttributeValue { S = TipoCategoria } }
```

`GetByIdAsync`/`UpdateAsync` — leitura direta de `CreatedByUserId`, sem
`TryGetValue` defensivo (sempre presente, tabela sem item legado):
```csharp
var createdByUserId = current.Item["CreatedByUserId"].S;
```
`UpdateAsync` **preserva** esse valor (lido do item atual, igual
`CreatedAt`) no `newItem` — o autor nunca muda numa edição (regra de
negócio da spec).

`QueryAsync`/`BuildQueryRequest` — filtro de `tipo` combinado ao
filtro de valor já existente (renomeado `BuildFilterExpression`):
```csharp
private static string? BuildFilterExpression(
    TransactionQueryFilter filter, Dictionary<string, string> names, Dictionary<string, AttributeValue> values)
{
    var conditions = new List<string>();

    if (filter.Tipo is not null)
    {
        names["#tipo"] = "Tipo";
        values[":tipo"] = new AttributeValue { S = filter.Tipo };
        conditions.Add("#tipo = :tipo");
    }

    if (filter.MinAmountInCents is not null) { /* inalterado */ }
    if (filter.MaxAmountInCents is not null) { /* inalterado */ }

    return conditions.Count == 0 ? null : string.Join(" AND ", conditions);
}
```
Via `FilterExpression` do próprio DynamoDB (não em memória) — ao
contrário do filtro de `tipo` de `Category` na FEAT-21, aqui não há
"ausente = default" pra aplicar antes: toda transação sempre tem
`Tipo` gravado (era `"despesa"` fixo mesmo antes desta feature), então
o atributo nunca está ausente e o `FilterExpression` funciona sem
ressalva.

`MapToTransactionQueryItem`:
```csharp
CreatedByUserId: item["CreatedByUserId"].S,
Tipo: item["Tipo"].S,
Date: DateOnly.ParseExact(item["Date"].S, DateFormat, CultureInfo.InvariantCulture), // era item["ExpenseDate"]
```

## Api-layer

### `CurrentAccountContext` — novo campo

```csharp
public sealed class CurrentAccountContext
{
    public string? AccountId { get; set; }
    public string? MembershipId { get; set; }
    public MembershipRole? Role { get; set; }
    public string? UserId { get; set; } // novo — mesmo userId já extraído do JWT por ResolveAccountEndpointFilter
}
```

`ResolveAccountEndpointFilter`: depois de `_currentAccount.Role = result.Value.Role;`,
adiciona `_currentAccount.UserId = userId;` (a variável `userId` já
existe no método, extraída da claim `sub` logo no início — só passa a
ser guardada, sem nova extração nem novo ponto de leitura do JWT).

### `TransactionEndpoints` (`Endpoints/TransactionEndpoints.cs`)

```csharp
var group = app.MapGroup("/transactions")
    .WithTags("Transactions")
    .RequireAuthorization()
    .AddEndpointFilter<ResolveAccountEndpointFilter>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

group.MapPost("/", RegisterTransaction)
    .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
    ...

group.MapGet("/", GetTransactions)...
group.MapGet("/{id}", GetTransactionById)...

group.MapPut("/{id}", UpdateTransaction)
    // Lancar agora passa no gate estático — posse (createdByUserId == chamador)
    // é checada dentro do Handler (ver Application-layer, decisão técnica 2).
    .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
    ...

group.MapDelete("/{id}", DeleteTransaction)
    .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
    ...
```

Handlers passam `currentAccount.UserId!` (novo) e, pra `PUT`/`DELETE`,
`currentAccount.Role!.Value` também:

```csharp
private static async Task<IResult> RegisterTransaction(
    RegisterTransactionRequest request, CurrentAccountContext currentAccount, ISender sender, CancellationToken ct)
{
    var command = new RegisterTransactionCommand(
        currentAccount.AccountId!, request.Description, request.AmountInCents,
        request.CategoryId, request.Tipo, request.Date, currentAccount.UserId!);
    var result = await sender.Send(command, ct);
    return result.ToHttpResult(value => Results.Created($"/transactions/{value.Id}", value));
}

private static async Task<IResult> UpdateTransaction(
    string id, UpdateTransactionRequest request, CurrentAccountContext currentAccount, ISender sender, CancellationToken ct)
{
    var command = new UpdateTransactionCommand(
        currentAccount.AccountId!, id, currentAccount.UserId!, currentAccount.Role!.Value,
        request.Description, request.AmountInCents, request.CategoryId, request.Tipo, request.Date);
    var result = await sender.Send(command, ct);
    return result.ToHttpResult(value => Results.Ok(value));
}

private static async Task<IResult> DeleteTransaction(
    string id, CurrentAccountContext currentAccount, ISender sender, CancellationToken ct)
{
    var command = new DeleteTransactionCommand(currentAccount.AccountId!, id, currentAccount.UserId!, currentAccount.Role!.Value);
    var result = await sender.Send(command, ct);
    return result.ToHttpResult(() => Results.NoContent());
}

private static async Task<IResult> GetTransactions(
    [AsParameters] GetTransactionsRequest request, CurrentAccountContext currentAccount, ISender sender, CancellationToken ct)
{
    var query = new GetTransactionsQuery(
        currentAccount.AccountId!, currentAccount.UserId!, NullIfEmpty(request.Tipo), NullIfEmpty(request.YearMonth),
        NullIfEmpty(request.CategoryId), NullIfEmpty(request.DateFrom), NullIfEmpty(request.DateTo),
        request.MinAmountInCents, request.MaxAmountInCents, NullIfEmpty(request.Cursor), request.Limit);
    var result = await sender.Send(query, ct);
    return result.ToHttpResult(value => Results.Ok(value));
}
```

```csharp
public record RegisterTransactionRequest(string Description, long AmountInCents, string CategoryId, string Tipo, DateOnly Date);
public record UpdateTransactionRequest(string Description, long AmountInCents, string CategoryId, string Tipo, DateOnly Date);
public record GetTransactionsRequest(
    string Tipo = "", string YearMonth = "", string CategoryId = "", string DateFrom = "", string DateTo = "",
    long? MinAmountInCents = null, long? MaxAmountInCents = null, string Cursor = "", int? Limit = null);
```

`Program.cs`: `app.MapExpenseEndpoints();` → `app.MapTransactionEndpoints();`.

### `AppJsonSerializerContext.cs`

Substituir as 6 entradas `Expense*`/`GetExpenses*` por
`Transaction*`/`GetTransactions*`:
```csharp
[JsonSerializable(typeof(RegisterTransactionRequest))]
[JsonSerializable(typeof(UpdateTransactionRequest))]
[JsonSerializable(typeof(RegisterTransactionResult))]
[JsonSerializable(typeof(UpdateTransactionResult))]
[JsonSerializable(typeof(GetTransactionsResult))]
[JsonSerializable(typeof(TransactionSummary))]
[JsonSerializable(typeof(GetTransactionsRequest))]
```
(`GetTransactionsRequest` é bindado via `[AsParameters]`, não passa
pelo `JsonSerializerContext` de fato — mantido na lista só por
paralelismo com o padrão já adotado pra `GetExpensesRequest`, que
também está listado hoje mesmo sem necessidade estrita; não alterar
esse hábito nesta feature.)

## Mapeamento de erros

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `tipo` ausente/vazio ou fora de `despesa`/`receita` (`POST`/`PUT`/`GET ?tipo=`) | `validation-error` | `Validation` | 400 |
| `categoryId` inexistente, de outra conta, ou de tipo divergente da transação | `validation-error` | `Validation` | 400 |
| `PUT`/`DELETE /transactions/{id}` por `Leitura` (gate estático) | `insufficient-permission` | `Forbidden` | 403 |
| `PUT`/`DELETE /transactions/{id}` por `Lancar` numa transação de outro autor (checagem no Handler) | `insufficient-permission` | `Forbidden` | 403 |
| `GET`/`PUT`/`DELETE /transactions/{id}` inexistente ou de outra conta | `not-found` | `NotFound` | 404 |
| `DELETE /categories/{id}` com transações vinculadas | `category-in-use` | `UnprocessableEntity` | 422 |

Todos reaproveitam `Error`/`ErrorType` já existentes — nenhum
`ErrorType` novo. `insufficient-permission` é o mesmo `Error` nos dois
casos da tabela (`MembershipErrors.InsufficientPermission`), só a
origem (filtro estático vs. Handler) muda.

## Recursos AWS

Nenhum recurso novo. `CreatedByUserId` é atributo regular (não
projetado em nenhum GSI) — reaproveita a tabela `GastosApp`, o `GSI1`
e o `GSI2` já provisionados, sem alteração em
`backend/infra/terraform/`. A troca do valor gravado em `Tipo` (de
constante pra variável) não muda schema nem índice — DynamoDB é
schemaless por item.

**Pré-requisito de deploy (runbook, fora do código, execução manual do
usuário — confirmado por ele dado o baixo volume de dados de teste
hoje):** a tabela `GastosApp` é recriada do zero em cada ambiente antes
do primeiro deploy desta feature — não só os itens de transação, a
tabela inteira (`Category`/`Membership`/`Account`/`AccountPointer`
também são perdidos). Local (`infra/README.md`, LocalStack/FEAT-18):
recriar o container e rodar os scripts de seed de novo. Hom/prod:
apagar e recriar a tabela via Terraform (`terraform destroy -target
aws_dynamodb_table.gastos_app && terraform apply`, ou script
equivalente de limpeza de itens) antes do deploy — mesmo padrão de
"runbook manual fora do código" já usado pelas FEAT-06/FEAT-07 pra
mudança de formato de chave.

## Plano de testes

Todo arquivo de teste listado abaixo é uma reescrita do equivalente
`*Expense*` já existente (ver mapa de renomeação), ajustado pros novos
campos/regras — não uma criação do zero, exceto onde marcado "novo".

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Domain/TransactionTests.cs` (era `ExpenseTests.cs`): `Create`/
  `Restore` com `tipo`/`createdByUserId`
- `Application/RegisterTransactionCommandValidatorTests.cs`: casos
  novos — `tipo` ausente/vazio/fora de `despesa`\|`receita` → inválido;
  `categoryId` de categoria com `tipo` divergente → inválido (mensagem
  igual à de categoria inexistente); `tipo` batendo com a categoria →
  válido
- `Application/UpdateTransactionCommandValidatorTests.cs` (mesmos
  casos, mirror)
- `Application/RegisterTransactionCommandHandlerTests.cs`: `Result`
  inclui `createdByUserId`/`createdByLabel="Você"` sempre
- `Application/UpdateTransactionCommandHandlerTests.cs`: **novos
  casos** — `CallerRole=Lancar` + `CreatedByUserId` igual ao chamador →
  sucesso; `CallerRole=Lancar` + `CreatedByUserId` diferente → Forbidden
  (`MembershipErrors.InsufficientPermission`), `UpdateAsync` do
  repositório mockado nunca chamado; `CallerRole=Total`/`Titular` numa
  transação de outro autor → sucesso (sem checagem de posse);
  `GetByIdAsync` retornando `null` → `NotFound` sem chamar `UpdateAsync`
- `Application/DeleteTransactionCommandHandlerTests.cs`: mesmos 4 casos
  espelhados pra exclusão
- `Application/GetTransactionByIdQueryHandlerTests.cs`: `createdByLabel`
  = `"Você"` quando `CreatedByUserId == CallerUserId`; e-mail do
  `Membership` retornado pelo mock quando é outro autor;
  `"Ex-membro"` quando `FindByAccountAndUserIdAsync` retorna `null`
  (autor removido da conta — ver "Decisões confirmadas com o usuário")
- `Application/GetTransactionsQueryHandlerTests.cs`: filtro `Tipo`
  repassado ao repositório mockado; **novo** — duas transações do
  mesmo `CreatedByUserId` (outro autor) resultam em só uma chamada a
  `FindByAccountAndUserIdAsync` (comprova o cache por página)
- `Application/GetTransactionsQueryValidatorTests.cs` (era
  `GetExpensesQueryValidatorTests.cs`): + caso de `tipo` inválido/nulo/
  válido
- `Infrastructure/DynamoDbTransactionRepositorySaveTests.cs` (**novo**
  — confirmado que não existe hoje `DynamoDbExpenseRepositorySaveTests.cs`;
  `SaveAsync` só era exercitado indiretamente via
  `RegisterExpenseCommandHandlerTests`/componente): `Tipo` gravado
  igual ao `transaction.Tipo` (não mais constante); `CreatedByUserId`
  sempre presente
- `Infrastructure/DynamoDbTransactionRepositoryGetByIdTests.cs`: `Tipo`
  aceitando tanto `"despesa"` quanto `"receita"` como item válido
  (`IsTransactionItem`); item com `Tipo="categoria"` continua rejeitado
  (proteção contra `id` de categoria); `Date` lido do atributo `Date`
  (não mais `ExpenseDate`)
- `Infrastructure/DynamoDbTransactionRepositoryUpdateTests.cs`:
  `CreatedByUserId` do item atual preservado no item novo (autor nunca
  muda numa edição)
- `Infrastructure/DynamoDbTransactionRepositoryDeleteTests.cs`:
  `ConditionExpression` nova (`#tipo <> :tipoCategoria`) — apagar um
  item `Tipo="receita"` funciona; apagar um item `Tipo="categoria"`
  continua bloqueado
- `Infrastructure/DynamoDbTransactionRepositoryQueryTests.cs`:
  `FilterExpression` inclui `Tipo` quando `filter.Tipo` informado,
  combinado com filtro de valor quando os dois estão presentes
- `Infrastructure/DynamoDbTransactionRepositoryExistsByCategoryTests.cs`
  (mirror direto, sem mudança de lógica)

### Component tests (`backend/tests/GastosApp.ComponentTests/Transactions/TransactionEndpointsTests.cs`, era `Expenses/ExpenseEndpointsTests.cs`)

Cobre as 20 user stories do `spec.md` fim a fim (mock de
`ITransactionRepository`/`ICategoryRepository`/`IMembershipRepository`
via `WebApplicationFactory`, ver FEAT-03): registro de despesa/receita;
`tipo` inválido/divergente da categoria → 400; `categoryId` inexistente/
de outra conta → 400; listagem sem filtro, com `tipo`, combinando
filtros; `GET /transactions/{id}` com `createdByLabel` "Você" e e-mail
de outro membro; `PUT`/`DELETE` por `Total`/`Titular` em transação
própria e de outro membro; `PUT`/`DELETE` por `Lancar` em transação
própria (sucesso) e de outro membro (403); `Leitura` bloqueado nos
três verbos de escrita; isolamento entre contas (404); 401 sem token;
`DELETE /categories/{id}` com transação vinculada → 422
(`category-in-use`).

`backend/tests/GastosApp.ComponentTests/Categories/CategoryEndpointsTests.cs`:
ajustar mock de dependência (`ITransactionRepository` no lugar de
`IExpenseRepository`) nos casos já existentes de `DELETE
/categories/{id}` bloqueado por uso — sem novo caso, só a troca do
tipo mockado.

## Critical Files

- `backend/src/GastosApp.Domain/Transactions/Transaction.cs` (novo; `Expense.cs` removido)
- `backend/src/GastosApp.Application/Common/Interfaces/ITransactionRepository.cs` + `TransactionQueryFilter/Item/Page.cs`
- `backend/src/GastosApp.Application/Common/Cursors/TransactionCursor*.cs`
- `backend/src/GastosApp.Application/Transactions/**` (Commands `RegisterTransaction`/`UpdateTransaction`/`DeleteTransaction`, Queries `GetTransactions`/`GetTransactionById`, `TransactionErrors.cs`, `Common/CreatedByLabelResolver.cs`)
- `backend/src/GastosApp.Application/Categories/Commands/DeleteCategory/DeleteCategoryCommand.cs` — troca de dependência
- `backend/src/GastosApp.Application/Categories/CategoryErrors.cs` — texto da mensagem
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Infrastructure/Transactions/DynamoDbTransactionRepository.cs` (novo; `Expenses/DynamoDbExpenseRepository.cs` removido)
- `backend/src/GastosApp.Api/Common/CurrentAccountContext.cs` — novo campo `UserId`
- `backend/src/GastosApp.Api/Common/ResolveAccountEndpointFilter.cs` — popular `UserId`
- `backend/src/GastosApp.Api/Endpoints/TransactionEndpoints.cs` (novo; `ExpenseEndpoints.cs` removido)
- `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`
- `backend/src/GastosApp.Api/Program.cs` — `MapTransactionEndpoints()`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Categories`/`Transactions`/`Members`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar a
  remoção completa de `/expenses` e o `/transactions` novo
  (`GET`/`POST`/`PUT`/`DELETE`, incluindo `/{id}`), os schemas com
  `tipo`/`date`/`createdByUserId`/`createdByLabel`, e o novo parâmetro
  de query `tipo`
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/LocalStack, idealmente com uma segunda conta
  Cognito convidada como `Lancar` pra exercitar a autorização por
  posse): registrar despesa e receita; tentar `tipo` divergente da
  categoria (400); listar com `?tipo=`; abrir detalhe de transação de
  outro membro (e-mail em `createdByLabel`); editar/excluir como
  `Lancar` uma transação própria (sucesso) e de outro membro (403);
  confirmar que `/expenses` não responde mais (404 de rota, não da
  aplicação)

## Decisões técnicas

1. **Sem atributo novo pro discriminador — `Tipo` do item DynamoDB vira
   diretamente o campo de negócio.** Ver Contexto técnico, ponto 1.
   Oposto da solução da FEAT-21 pra `Category` (lá precisou de
   `TipoLancamento` novo); aqui os dois valores de negócio já são
   compatíveis com o papel de discriminador.
2. **Checagem de posse (`Lancar` só edita/exclui o que criou) no
   Handler do Command, não em `RoleEndpointFilters`.** O filtro
   estático não tem acesso ao recurso; `Lancar` passa no gate de rota e
   a posse é validada depois de um `GetByIdAsync` que o Handler já
   precisa fazer. Aceita uma leitura extra em `UpdateAsync`/
   `DeleteAsync` (que fazem seu próprio lookup interno) — custo
   desprezível no volume esperado.
3. **Tabela `GastosApp` recriada do zero antes do deploy** (confirmado
   com o usuário) — elimina qualquer necessidade de tratar itens
   antigos como caso especial nesta feature. Como consequência direta,
   o atributo DynamoDB da data é renomeado de `ExpenseDate` pra `Date`
   (acompanhando o Domain/API) — sem esse pré-requisito, o rename
   exigiria um `TryGetValue` de fallback (mesmo padrão já usado pela
   FEAT-21 pra `TipoLancamento`); com a tabela recriada, não há item
   antigo a ler, então o rename é gratuito.
4. **Autor removido da conta (`DELETE /members`, FEAT-20) mostra
   `createdByLabel: "Ex-membro"`** — cenário concreto hoje, já que
   `DELETE /members` não bloqueia a remoção de um membro com
   transações lançadas. Confirmado com o usuário como **débito técnico
   registrado no roadmap** (ver `backend/docs/roadmap.md`): no futuro,
   um membro com transações lançadas não poderá ser excluído — vira
   `Inativo` em vez de removido, e continua aparecendo como autor nas
   transações que já criou. Essa feature futura não vai exigir mudança
   neste resolver (`FindByAccountAndUserIdAsync` já não filtra por
   `Status`, então encontraria o `Membership` `Inativo` normalmente) —
   só o cenário que hoje cai em `"Ex-membro"` deixa de acontecer.
5. **Sem enum para `Tipo`** — mesma decisão da FEAT-21 pra
   `Category.Tipo`, resolvendo o ponto que aquele `plan.md` tinha
   deixado em aberto.
6. **`MembershipErrors.InsufficientPermission` reaproveitado para o
   403 de posse** (Handler) em vez de um `TransactionErrors` próprio —
   mesmo código/mensagem/tipo, evita duplicação de um `Error`
   semanticamente idêntico.
7. **Duas leituras (`GetByIdAsync` + o lookup interno de `UpdateAsync`/
   `DeleteAsync`) em toda edição/exclusão** — confirmado com o usuário
   como aceitável (custo desprezível em DynamoDB on-demand no volume
   de uso pessoal do projeto), em vez de mudar a interface do
   repositório pra expor um único método que já devolva o estado
   anterior.

## Decisões confirmadas com o usuário (revisão pós-plan)

Os três pontos abertos da primeira versão deste plan foram revisados
com o usuário e resolvidos assim, refletidos nas seções acima:

1. Tabela `GastosApp` recriada do zero antes do deploy — decisão 3.
2. Membro removido continuando a aparecer como autor de transações
   passadas vira débito técnico explícito no roadmap (não implementado
   nesta feature); enquanto não implementado, `createdByLabel` cai em
   `"Ex-membro"` — decisão 4.
3. Duas leituras por edição/exclusão pra viabilizar a checagem de posse
   — aceito, decisão 7.
