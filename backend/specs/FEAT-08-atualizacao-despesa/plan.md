# Plan: FEAT-08 — Atualização de Despesa — Plano Técnico

## Contexto técnico

`spec.md` já foi aprovado e define `PUT /expenses/{id}` como substituição
completa (todos os campos obrigatórios, mesma validação do
`POST /expenses` da FEAT-04), retornando 200 com a despesa atualizada e
404 unificado (sem diferenciar "inexistente" de "de outro usuário",
mesmo padrão da FEAT-07).

Mesmo desafio técnico da FEAT-07: a SK da tabela `GastosApp` é
`TXN#{yyyy-MM-dd}#{id}` — a data faz parte da chave física do item. Se o
`expenseDate` mudar na atualização, a SK (e o `GSI1SK`) mudam junto, o
que exige excluir o item antigo e gravar um novo (não é um `UpdateItem`
in-place). O `GSI2` (`GSI2PK = ID#{id}`, criado na FEAT-07) resolve a
localização do item a partir do `id`, mas sua projeção é `KEYS_ONLY` —
só devolve `PK`/`SK`, não os demais atributos (`CreatedAt` precisa ser
preservado na resposta). Por isso o fluxo de atualização precisa de um
`GetItem` adicional (usando o `PK`/`SK` resolvidos pelo `GSI2`) para
obter o item completo antes de decidir como persistir a mudança.

## Decisões técnicas

1. **Fluxo de atualização no repositório** (`DynamoDbExpenseRepository`,
   mirror de `DeleteAsync` até a checagem de posse):
   - `Query` no `GSI2` por `GSI2PK = ID#{expenseId}` (`Limit = 1`) → `PK`/`SK` reais
   - Checagem de posse: `PK` retornado deve ser `USER#{userId}`; se não bater
     (ou a Query não retornar nada), tratar como não encontrado — mesma
     lógica da FEAT-07, garante o 404 sem vazar existência (US5)
   - `GetItem(PK, SK)` para obter o item completo (principalmente
     `CreatedAt`, que não está no `GSI2` por causa da projeção `KEYS_ONLY`)
   - Se o item não existir mais nesse ponto (corrida entre a Query e o
     GetItem), tratar como não encontrado — mesma idempotência de
     exclusão concorrente já aceita na FEAT-07
2. **Persistência da atualização — dois caminhos, decididos pela data**:
   - **`expenseDate` (dia) não muda**: `SK`/`GSI1SK` continuam os mesmos
     → `PutItem` simples sobrescrevendo o item na mesma chave (mesmo
     padrão de `SaveAsync`), com os novos valores de
     `Description`/`AmountInCents`/`Category`/`ExpenseDate`/`GSI1PK`
     (`GSI1PK` muda se a categoria mudou, mas isso não afeta a chave
     física do item — é só um atributo regular do ponto de vista da
     tabela base)
   - **`expenseDate` (dia) muda**: `SK`/`GSI1SK` mudam → não dá para
     `UpdateItem` in-place (mudaria a chave primária do item, o que o
     DynamoDB não permite). Usa `TransactWriteItems` com dois itens:
     `Delete` do item antigo (`PK`/`SK` originais, `ConditionExpression:
     attribute_exists(PK)`) + `Put` do item novo (`PK` igual, `SK` novo,
     demais atributos atualizados) — atômico, evita ficar com os dois
     itens (duplicado) ou nenhum (perdido) em caso de falha parcial.
     Primeiro uso de `TransactWriteItems` no projeto; não introduz
     recurso AWS novo, é só outro tipo de chamada da mesma tabela
     `GastosApp` já existente.
3. **`Id`, `UserId` e `CreatedAt` nunca mudam**: vêm do item já persistido
   (obtido no `GetItem` do passo 1), não do request — `Expense.Restore`
   (novo factory no Domain) reconstrói a entidade preservando esses três
   campos e aplicando os novos valores de
   `Description`/`AmountInCents`/`Category`/`ExpenseDate`.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | `Expense` ganha `Expense.Restore(id, userId, description, amountInCents, category, expenseDate, createdAt)` — factory para reconstruir a entidade a partir de dados já persistidos (sem gerar novo `Id`/`CreatedAt`, diferente de `Create`) |
| Application | Novo `UpdateExpenseCommand`+Handler+`UpdateExpenseResult` (mirror de `RegisterExpenseCommand.cs`); `UpdateExpenseCommandValidator` (mirror de `RegisterExpenseCommandValidator.cs`); `IExpenseRepository` ganha `UpdateAsync` |
| Infrastructure | `DynamoDbExpenseRepository`: novo `UpdateAsync` (Query GSI2 → checagem de posse → GetItem → Put in-place ou TransactWriteItems Delete+Put conforme a data) |
| Api | `ExpenseEndpoints`: `MapPut("/{id}", UpdateExpense)` |
| AWS/Terraform | Nenhuma mudança — reaproveita a tabela `GastosApp` e o `GSI2` já provisionados na FEAT-07 |

## Contratos Application-layer

### `Expense.Restore` (novo factory em `backend/src/GastosApp.Domain/Expenses/Expense.cs`)

```csharp
public static Expense Restore(
    string id, string userId, string description, long amountInCents,
    ExpenseCategory category, DateOnly expenseDate, DateTimeOffset createdAt)
{
    return new Expense(id, userId, description, amountInCents, category, expenseDate, createdAt);
}
```
Usa o mesmo construtor privado de `Create`, só não gera novo `Id`/`CreatedAt` — recebe os valores já existentes do item persistido.

### `UpdateExpenseCommand` (novo: `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommand.cs`, mirror de `RegisterExpenseCommand.cs`)

```csharp
public sealed record UpdateExpenseCommand(
    string UserId,
    string ExpenseId,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate) : ICommand<Result<UpdateExpenseResult>>;

public sealed class UpdateExpenseCommandHandler : ICommandHandler<UpdateExpenseCommand, Result<UpdateExpenseResult>>
{
    private readonly IExpenseRepository _expenseRepository;

    public UpdateExpenseCommandHandler(IExpenseRepository expenseRepository) => _expenseRepository = expenseRepository;

    public async ValueTask<Result<UpdateExpenseResult>> Handle(UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        var category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);

        var updated = await _expenseRepository.UpdateAsync(
            command.UserId, command.ExpenseId, command.Description, command.AmountInCents, category, command.ExpenseDate, cancellationToken);

        return updated is null
            ? Result.Failure<UpdateExpenseResult>(ExpenseErrors.NotFound)
            : Result.Success(UpdateExpenseResult.FromExpense(updated));
    }
}

public record UpdateExpenseResult(
    string Id, string Description, long AmountInCents, string Category, DateOnly ExpenseDate, DateTimeOffset CreatedAt)
{
    public static UpdateExpenseResult FromExpense(Expense expense) => new(
        expense.Id, expense.Description, expense.AmountInCents,
        expense.Category.ToString(), expense.ExpenseDate, expense.CreatedAt);
}
```

### `UpdateExpenseCommandValidator` (mirror exato de `RegisterExpenseCommandValidator.cs`)

Mesmas regras: `Description` (`NotEmpty`, `MaximumLength(200)`), `AmountInCents` (`GreaterThan(0)`), `Category` (`Enum.TryParse`/`Enum.IsDefined`). `ExpenseId` não é validado (vem do path, sempre presente pela própria rota).

### `IExpenseRepository` (adicionar método)

```csharp
public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
    Task<Expense?> UpdateAsync(
        string userId, string expenseId, string description, long amountInCents,
        ExpenseCategory category, DateOnly expenseDate, CancellationToken cancellationToken = default);
}
```
Retorna `Expense?` (não `Result`) — `null` sinaliza não encontrado/não
pertence ao usuário, seguindo o mesmo racional de `DeleteAsync` retornar
`bool`: tradução para `Result`/`Error` é responsabilidade do Handler.

## Infrastructure-layer — `DynamoDbExpenseRepository.UpdateAsync`

```csharp
public async Task<Expense?> UpdateAsync(
    string userId, string expenseId, string description, long amountInCents,
    ExpenseCategory category, DateOnly expenseDate, CancellationToken cancellationToken = default)
{
    var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        IndexName = Gsi2Index,
        KeyConditionExpression = "GSI2PK = :gsi2pk",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":gsi2pk"] = new AttributeValue { S = $"ID#{expenseId}" }
        },
        Limit = 1
    }, cancellationToken);

    if (lookup.Items.Count == 0)
        return null;

    var pk = lookup.Items[0]["PK"].S;
    var oldSk = lookup.Items[0]["SK"].S;

    if (pk != $"USER#{userId}")
        return null;

    var current = await _dynamoDbClient.GetItemAsync(new GetItemRequest
    {
        TableName = _options.TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = pk },
            ["SK"] = new AttributeValue { S = oldSk }
        }
    }, cancellationToken);

    if (!current.IsItemSet)
        return null; // corrida: item excluído entre a Query e o GetItem

    var createdAt = DateTimeOffset.Parse(current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    var newDay = expenseDate.ToString(DateFormat);
    var newSk = $"TXN#{newDay}#{expenseId}";

    var newItem = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = pk },
        ["SK"] = new AttributeValue { S = newSk },
        ["GSI1PK"] = new AttributeValue { S = $"{pk}#{category}" },
        ["GSI1SK"] = new AttributeValue { S = $"{newDay}#{expenseId}" },
        ["GSI2PK"] = new AttributeValue { S = $"ID#{expenseId}" },
        ["Description"] = new AttributeValue { S = description },
        ["AmountInCents"] = new AttributeValue { N = amountInCents.ToString() },
        ["Category"] = new AttributeValue { S = category.ToString() },
        ["ExpenseDate"] = new AttributeValue { S = newDay },
        ["Tipo"] = new AttributeValue { S = "despesa" },
        ["CreatedAt"] = new AttributeValue { S = createdAt.ToString("O") }
    };

    if (newSk == oldSk)
    {
        // Data não mudou: mesma chave física, PutItem simples sobrescreve o item (mesmo padrão de SaveAsync).
        await _dynamoDbClient.PutItemAsync(new PutItemRequest { TableName = _options.TableName, Item = newItem }, cancellationToken);
    }
    else
    {
        // Data mudou: SK muda, não dá pra UpdateItem in-place. Delete+Put atômico via TransactWriteItems.
        await _dynamoDbClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _options.TableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            ["PK"] = new AttributeValue { S = pk },
                            ["SK"] = new AttributeValue { S = oldSk }
                        },
                        ConditionExpression = "attribute_exists(PK)"
                    }
                },
                new TransactWriteItem
                {
                    Put = new Put { TableName = _options.TableName, Item = newItem }
                }
            ]
        }, cancellationToken);
    }

    return Expense.Restore(expenseId, userId, description, amountInCents, category, expenseDate, createdAt);
}
```
`TransactCanceledException` (ex.: item excluído entre o `GetItem` e o
`TransactWriteItems`) não é capturada especificamente — sobe como
exceção não mapeada → 500 pelo `GlobalExceptionHandler`, mesmo
comportamento de qualquer falha inesperada de infraestrutura nas
features anteriores (janela de corrida extremamente estreita, não
justifica lógica de retry nesta feature).

## Api-layer — endpoint

```csharp
group.MapPut("/{id}", UpdateExpense);   // dentro do MapGroup("/expenses").RequireAuthorization() já existente

private static async Task<IResult> UpdateExpense(
    string id, UpdateExpenseRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
{
    var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    var command = new UpdateExpenseCommand(
        userId!, id, request.Description, request.AmountInCents, request.Category, request.ExpenseDate);

    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(value => Results.Ok(value));
}

public record UpdateExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate);
```
Mesmo shape de `RegisterExpenseRequest`. `RequireAuthorization()` do
grupo já cobre 401 sem token (US2), sem código extra.

## Mapeamento de erros

| Cenário | `ErrorType` | Status HTTP | `type` (slug) |
|---|---|---|---|
| Descrição ausente/vazia/> 200 chars | `Validation` | 400 | `validation-error` |
| Valor <= 0 | `Validation` | 400 | `validation-error` |
| Categoria fora do enum | `Validation` | 400 | `validation-error` |
| Sem token / token inválido | — (middleware JWT) | 401 | `unauthorized` |
| Despesa inexistente | `NotFound` | 404 | `not-found` (reaproveita `ExpenseErrors.NotFound` da FEAT-07) |
| Despesa de outro usuário | `NotFound` | 404 | `not-found` (mesmo slug, não diferencia — US5) |
| Falha inesperada (DynamoDB indisponível, transação cancelada etc.) | — (exceção não mapeada → `GlobalExceptionHandler`) | 500 | `internal-server-error` |

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/UpdateExpenseCommandValidatorTests.cs` — mirror de
  `RegisterExpenseCommandValidatorTests.cs` (se existir) ou dos casos já
  cobertos no Component Test do registro: descrição vazia/> 200 chars,
  valor <= 0, categoria fora do enum
- `Application/UpdateExpenseCommandHandlerTests.cs` — `UpdateAsync`
  retorna `Expense` → `Result.Success` com `UpdateExpenseResult`
  correspondente; retorna `null` → `Result.Failure` com
  `ErrorType.NotFound`/`not-found`; `Received(1).UpdateAsync(userId,
  expenseId, description, amountInCents, category, expenseDate, ...)`
- `Infrastructure/DynamoDbExpenseRepositoryUpdateTests.cs` (mock de
  `IAmazonDynamoDB` via NSubstitute) cobrindo: Query no GSI2 sem
  resultado → `null` sem chamar `GetItemAsync`/`PutItemAsync`/
  `TransactWriteItemsAsync`; item de outro usuário → `null` sem
  persistir; `GetItemAsync` retorna vazio (corrida) → `null`; data
  inalterada → `PutItemAsync` chamado com a mesma `SK`, `TransactWriteItemsAsync`
  **não** chamado; data alterada → `TransactWriteItemsAsync` chamado com
  `Delete` (chave antiga, `ConditionExpression`) + `Put` (chave nova),
  `PutItemAsync` **não** chamado; `CreatedAt` do resultado preservado do
  item original em ambos os casos

### Component tests (`backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs`, mockando `IExpenseRepository.UpdateAsync`)

Cobrindo os 8 critérios de aceite do `spec.md`:
- Sucesso: `UpdateAsync` retorna `Expense` → 200 com o corpo atualizado (US1)
- Sem header de autenticação → 401, `UpdateAsync` **não** chamado (US2)
- Descrição vazia, valor <= 0, categoria fora do enum → 400 cada, sem
  chamar `UpdateAsync` (US3, mesmo padrão de `[Theory]`/`[InlineData]`
  ou testes isolados usado no registro)
- `UpdateAsync` retorna `null` → 404, `type` = `.../not-found` (US4/US5)
- Smoke test de falha inesperada: `UpdateAsync` lança exceção → 500

## Critical Files

- `backend/src/GastosApp.Domain/Expenses/Expense.cs` — adicionar `Restore`
- `backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs` — adicionar `UpdateAsync`
- `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommand.cs` (novo)
- `backend/src/GastosApp.Application/Expenses/Commands/UpdateExpense/UpdateExpenseCommandValidator.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs` — `UpdateAsync` (novo)
- `backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs` — `MapPut("/{id}", UpdateExpense)` + `UpdateExpenseRequest`
- `backend/tests/GastosApp.UnitTests/Application/UpdateExpenseCommandValidatorTests.cs` (novo)
- `backend/tests/GastosApp.UnitTests/Application/UpdateExpenseCommandHandlerTests.cs` (novo)
- `backend/tests/GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryUpdateTests.cs` (novo)
- `backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs` — cenários PUT adicionados

## Verificação

- `dotnet build backend/GastosApp.sln` — confirma que o novo
  `ICommand<Result<UpdateExpenseResult>>` compila e o source generator
  do Mediator descobre o novo handler
- `dotnet test backend/GastosApp.sln` — suíte completa (Unit + Component)
  cobrindo os cenários acima
- Smoke manual (opcional, contra AWS real): registrar uma despesa,
  atualizar via `PUT /expenses/{id}` mudando só a categoria (espera 200,
  confirma via `GET /expenses`), depois atualizar mudando a data (espera
  200, confirma que a despesa aparece na nova data e não na antiga via
  `GET /expenses?dateFrom=...&dateTo=...`)

## Pontos que precisam de confirmação antes do `/tasks`

1. **`TransactWriteItems` para o caso de mudança de data**: é o primeiro
   uso desse tipo de chamada no projeto (até então só `PutItem`/`Query`/
   `DeleteItem`/`GetItem`). Custo é ~2x uma escrita normal (WCU dobrado
   por operação transacional), aceitável no volume pessoal do projeto —
   confirmar que não há objeção a essa escolha em vez de um Delete+Put
   não atômico (mais simples, porém com risco de duplicar ou perder o
   item em caso de falha entre as duas chamadas).
2. **`GetItem` extra por causa do `GSI2` ser `KEYS_ONLY`**: alternativa
   seria mudar a projeção do `GSI2` para incluir `CreatedAt` (ou `ALL`),
   evitando essa leitura extra, mas isso exigiria recriar o índice (não
   dá para alterar projection type in-place) — o `GSI2` acabou de ser
   criado manualmente na FEAT-07. Proponho manter `KEYS_ONLY` e pagar o
   `GetItem` extra (mais simples, sem mexer em infra já provisionada) —
   confirmar que está de acordo.
