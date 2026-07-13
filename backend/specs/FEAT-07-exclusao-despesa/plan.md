# Plan: FEAT-07 — Exclusão de Despesa — Plano Técnico

## Contexto técnico

`spec.md` já foi aprovado e define `DELETE /expenses/{id}` retornando 204
sem corpo no sucesso e 404 (sem diferenciar "inexistente" de "pertence a
outro usuário") em qualquer falha de resolução. O desafio técnico central:
a chave primária da tabela `GastosApp` é `PK=USER#{userId}` /
`SK=TXN#{yyyy-MM-dd}#{id}` (granularidade diária desde a FEAT-06) — a data
faz parte da `SK`, mas o endpoint só recebe o `id`. `DeleteItem` do
DynamoDB exige a chave exata (`PK`+`SK`), então é preciso resolver essa
chave antes de excluir.

## Decisão confirmada com o usuário

**Novo GSI2 por id, projeção `KEYS_ONLY`**: `GSI2PK = ID#{id}` (índice
hash-only, sem range key — `id` já é globalmente único, gerado como
`Guid.NewGuid()` em `Expense.Create`). Fluxo de exclusão:

1. `Query` no `GSI2` por `GSI2PK = ID#{id}` → devolve `PK`/`SK` reais do
   item (a projeção `KEYS_ONLY` inclui automaticamente a chave primária da
   tabela base).
2. **Checagem de posse obrigatória em código**: o `GSI2` não é
   particionado por usuário, então o `PK` devolvido deve ser comparado com
   `USER#{userId}` do chamador autenticado. Se não bater (ou se a Query não
   retornar nada), tratar como não encontrado — nunca prosseguir para o
   `DeleteItem`. É isso que implementa a regra "404 para despesa de outro
   usuário" da spec (US4), sem vazar a existência do item.
3. `DeleteItem` com `PK`/`SK` exatos e `ConditionExpression:
   attribute_exists(PK)` (defesa contra corrida — item já excluído entre a
   Query e o Delete faz a condição falhar, tratado como não encontrado, não
   como erro 500).

Trade-off aceito explicitamente pelo usuário: custo de escrita adicional
(mais um atributo indexado por item) e um novo recurso AWS (GSI2, mudança
de Terraform), em troca de lookup O(1) em vez de varrer toda a partição do
usuário a cada exclusão.

**Itens já persistidos (FEAT-04/06) não têm `GSI2PK`** e não serão
encontrados pela Query do passo 1 até serem migrados — ver runbook no
final deste plano (mesmo padrão adotado na FEAT-06 para a migração de SK).

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Nada |
| Application | Novo `DeleteExpenseCommand`+Handler (sem Result de valor, só `Result`); `ExpenseErrors.NotFound`; `IExpenseRepository` ganha `DeleteAsync` |
| Infrastructure | `DynamoDbExpenseRepository`: `SaveAsync` grava `GSI2PK`; novo `DeleteAsync` (Query GSI2 → checagem de posse → DeleteItem condicional) |
| Api | `ExpenseEndpoints`: `MapDelete("/{id}", DeleteExpense)` |
| AWS/Terraform | Novo GSI2 na tabela `GastosApp` (`backend/infra/terraform/dynamodb.tf`) — mudança explicitamente solicitada pelo usuário |

## Contratos Application-layer

### `DeleteExpenseCommand` (novo: `backend/src/GastosApp.Application/Expenses/Commands/DeleteExpense/DeleteExpenseCommand.cs`)

```csharp
public sealed record DeleteExpenseCommand(string UserId, string ExpenseId) : ICommand<Result>;

public sealed class DeleteExpenseCommandHandler : ICommandHandler<DeleteExpenseCommand, Result>
{
    private readonly IExpenseRepository _expenseRepository;

    public DeleteExpenseCommandHandler(IExpenseRepository expenseRepository) => _expenseRepository = expenseRepository;

    public async ValueTask<Result> Handle(DeleteExpenseCommand command, CancellationToken cancellationToken)
    {
        var deleted = await _expenseRepository.DeleteAsync(command.UserId, command.ExpenseId, cancellationToken);
        return deleted ? Result.Success() : ExpenseErrors.NotFound();
    }
}
```
Sem validator dedicado: `ExpenseId` vem do path (sempre uma string
não-vazia — rota não casa sem segmento), não há regra de negócio adicional
a validar. Mesmo racional de `RegisterExpenseCommand` não validar
`UserId` (garantido pelo `RequireAuthorization()`).

### `ExpenseErrors` (novo: `backend/src/GastosApp.Application/Expenses/ExpenseErrors.cs`, mesmo padrão de `Auth/AuthErrors.cs`)

```csharp
public static class ExpenseErrors
{
    public static Error NotFound() => Error.NotFound("not-found", "Despesa não encontrada.");
}
```
Primeiro uso de `ErrorType.NotFound`/`Error.NotFound` no projeto — o
mapeamento para 404 já existe em `ResultHttpExtensions.cs`, nenhuma
mudança necessária ali.

### `IExpenseRepository` (adicionar método)

```csharp
public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
}
```
Retorna `bool` (não `Result`) — segue o padrão de `SaveAsync`/`QueryAsync`,
que também não retornam `Result`: a tradução para `Result`/`Error` é
responsabilidade exclusiva do Handler (Application), não do repositório
(Infrastructure).

## Infrastructure-layer — `DynamoDbExpenseRepository`

### `SaveAsync` — único diff: grava `GSI2PK`

```csharp
["GSI2PK"] = new AttributeValue { S = $"ID#{expense.Id}" },
```
(demais atributos inalterados.)

### `DeleteAsync` — novo

```csharp
public async Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default)
{
    var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        IndexName = "GSI2",
        KeyConditionExpression = "GSI2PK = :gsi2pk",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":gsi2pk"] = new AttributeValue { S = $"ID#{expenseId}" }
        },
        Limit = 1
    }, cancellationToken);

    if (lookup.Items.Count == 0)
        return false;

    var pk = lookup.Items[0]["PK"].S;
    var sk = lookup.Items[0]["SK"].S;

    if (pk != $"USER#{userId}")
        return false; // despesa existe, mas é de outro usuário — tratado como não encontrada (US4)

    try
    {
        await _dynamoDbClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = pk },
                ["SK"] = new AttributeValue { S = sk }
            },
            ConditionExpression = "attribute_exists(PK)"
        }, cancellationToken);
        return true;
    }
    catch (ConditionalCheckFailedException)
    {
        return false; // exclusão concorrente entre a Query e o Delete — idempotência (spec: 2ª exclusão retorna 404)
    }
}
```
Query no `GSI2` usa só a hash key (`GSI2PK`), compatível com a regra "sem
Scan — apenas Query com PK ou GSI".

## Api-layer — endpoint

```csharp
group.MapDelete("/{id}", DeleteExpense);   // dentro do MapGroup("/expenses").RequireAuthorization() já existente

private static async Task<IResult> DeleteExpense(
    string id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
{
    var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var command = new DeleteExpenseCommand(userId!, id);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(() => Results.NoContent());
}
```
`ToHttpResult(Func<IResult> onSuccess)` (overload não-genérico de
`Result`, já existente em `ResultHttpExtensions.cs`) — mesmo mecanismo
usado se algum endpoint sem valor de retorno precisar mapear sucesso.
`RequireAuthorization()` do grupo já cobre 401 sem token (US2), sem código
extra.

## Mapeamento de erros

| Cenário | `ErrorType` | Status HTTP | `type` (slug) |
|---|---|---|---|
| Sucesso | — | 204 | — |
| Sem token / token inválido | — (middleware JWT) | 401 | `unauthorized` |
| Despesa inexistente | `NotFound` | 404 | `not-found` |
| Despesa de outro usuário | `NotFound` | 404 | `not-found` (mesmo slug, não diferencia — US4) |
| Falha inesperada (DynamoDB indisponível etc.) | — (exceção não mapeada → `GlobalExceptionHandler`) | 500 | `internal-server-error` |

## Recursos AWS afetados

- **Novo GSI2** na tabela `GastosApp` (`backend/infra/terraform/dynamodb.tf`):
  ```hcl
  attribute {
    name = "GSI2PK"
    type = "S"
  }

  global_secondary_index {
    name            = "GSI2"
    hash_key        = "GSI2PK"
    projection_type = "KEYS_ONLY"
  }
  ```
  Mudança explicitamente solicitada pelo usuário durante este `/plan`.
  Nenhum outro recurso AWS (Cognito, Parameter Store) é afetado.

## Runbook de migração manual (referência — execução fora deste código)

Itens já persistidos (FEAT-04/06) não têm `GSI2PK` e não serão encontrados
pela exclusão via GSI2 até serem migrados:
- Para cada item existente: `UpdateItem` adicionando `GSI2PK = ID#{id}`
  (o `{id}` vem do sufixo da própria `SK`, `TXN#{data}#{id}`) — operação
  aditiva, não precisa de `PutItem`/`DeleteItem` como a migração de SK da
  FEAT-06.
- Enumerar via `Query` por `PK=USER#<userId>` (userId conhecido, informado
  manualmente) — nunca `Scan`.
- Até a migração ser executada, tentar excluir uma despesa antiga
  resulta em 404 (não é encontrada via GSI2) mesmo sendo do usuário
  correto — comportamento aceito como consequência do runbook manual,
  mesmo padrão já aceito na FEAT-06 para o formato de SK.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/DeleteExpenseCommandHandlerTests.cs` — `DeleteAsync` retorna
  `true` → `Result.Success()`; retorna `false` → `Result.Failure` com
  `ErrorType.NotFound` e código `not-found`; `Received(1).DeleteAsync(userId, expenseId, ...)`
- `Infrastructure/DynamoDbExpenseRepositoryDeleteTests.cs` (mock de
  `IAmazonDynamoDB` via NSubstitute) — Query no GSI2 sem resultado → `false`
  sem chamar `DeleteItemAsync`; Query retorna item de `PK` diferente do
  `userId` informado → `false` sem chamar `DeleteItemAsync` (US4); Query
  retorna item do mesmo usuário → `DeleteItemAsync` chamado com `PK`/`SK`
  exatos e `ConditionExpression`, retorna `true`; `DeleteItemAsync` lança
  `ConditionalCheckFailedException` → `false` (não propaga exceção)

### Component tests (`backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs`, mockando `IExpenseRepository.DeleteAsync`)

Cobrindo os 6 critérios de aceite do `spec.md`:
- Sucesso: `DeleteAsync` retorna `true` → 204 sem corpo (US1)
- Sem header de autenticação → 401, `DeleteAsync` **não** chamado (US2)
- `DeleteAsync` retorna `false` → 404, `type` = `.../not-found` (US3 e US4,
  mesmo teste HTTP — a diferenciação de causa é interna ao repositório,
  já coberta pelos unit tests de Infrastructure)
- Duas chamadas sequenciais ao mesmo `id`: mock configurado para `true` na
  primeira e `false` na segunda → 204 depois 404 (US1 + regra de
  idempotência da spec)
- Smoke test de falha inesperada: `DeleteAsync` configurado para lançar
  exceção → 500 (`type` = `.../internal-server-error`), mesmo padrão dos
  demais endpoints

## Critical Files

- `backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs` — adicionar `DeleteAsync`
- `backend/src/GastosApp.Application/Expenses/ExpenseErrors.cs` (novo)
- `backend/src/GastosApp.Application/Expenses/Commands/DeleteExpense/DeleteExpenseCommand.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs` — `SaveAsync` (grava `GSI2PK`) + `DeleteAsync` (novo)
- `backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs` — `MapDelete("/{id}", DeleteExpense)`
- `backend/infra/terraform/dynamodb.tf` — novo `GSI2`
- `backend/tests/GastosApp.UnitTests/Application/DeleteExpenseCommandHandlerTests.cs` (novo)
- `backend/tests/GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryDeleteTests.cs` (novo)
- `backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs` — cenários DELETE adicionados

## Verificação

- `dotnet build backend/GastosApp.sln` — confirma que o novo `ICommand<Result>`
  compila e o source generator do Mediator descobre o novo handler
- `dotnet test backend/GastosApp.sln` — suíte completa (Unit + Component)
  cobrindo os cenários acima
- `terraform plan` em `backend/infra/terraform/` — confirma que a única
  mudança é a adição do `GSI2` (sem `force replacement` da tabela)
- Smoke manual (opcional, contra AWS real): registrar uma despesa via
  `POST /expenses`, confirmar via `GET /expenses`, excluir via
  `DELETE /expenses/{id}` (espera 204), confirmar que sumiu do
  `GET /expenses`, tentar excluir de novo (espera 404)

## Pontos que precisam de confirmação antes do `/tasks`

1. **Terraform**: aplicar o novo `GSI2` na tabela real (`terraform apply`)
   é uma operação fora do escopo deste plano/código — confirmar que a
   aplicação manual (ou via pipeline, se existir) fica a cargo do usuário
   antes dos testes manuais contra AWS real.
2. **Runbook de migração do `GSI2PK`** para despesas já persistidas (antes
   desta feature) é referência, não é executado por código — confirmar que
   está de acordo, assim como foi feito na FEAT-06 para a migração de SK.
3. **Nome do arquivo/pasta**: `Commands/DeleteExpense/DeleteExpenseCommand.cs`
   segue o padrão de `Commands/RegisterExpense/RegisterExpenseCommand.cs`
   (Command+Handler+eventuais tipos no mesmo arquivo) — como `DeleteExpenseCommand`
   não tem um `Result<T>` próprio, não há um "Result record" separado a
   nomear, apenas confirmar que não há objeção ao padrão.
