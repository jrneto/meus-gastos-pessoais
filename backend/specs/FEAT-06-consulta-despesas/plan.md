# Plan: FEAT-06 — Consulta de Despesas

## Contexto

`spec.md` já foi aprovado e define `GET /expenses` com filtros combináveis
(`yearMonth`, `category`, `dateFrom`/`dateTo`,
`minAmountInCents`/`maxAmountInCents`), paginação por cursor opaco e
ordenação por `expenseDate` desc (`createdAt` desc como desempate). A spec
identificou que a SK atual da tabela `GastosApp` (`TXN#{yyyy-MM}#{uuid}`)
só é granular por mês, não por dia — insuficiente para ordenar
cronologicamente os resultados — e definiu que a chave será ajustada para
granularidade diária como parte desta feature. Este plano traduz o
contrato de negócio da spec em contratos técnicos: assinaturas de
Query/Handler, DTOs, padrão de acesso ao DynamoDB e testes, respeitando as
regras da constitution (sem `Scan`, `userId` só do JWT, Result Pattern,
Mediator, validação via pipeline).

## Decisões confirmadas com o usuário

1. **SK/GSI1SK migram para granularidade diária**: `TXN#{yyyy-MM-dd}#{id}`
   (SK) e `{yyyy-MM-dd}#{id}` (GSI1SK). Mantém `begins_with` por mês
   funcionando e habilita comparação nativa por intervalo de datas (mais
   barato que `FilterExpression`). Nenhum novo atributo, GSI ou mudança de
   Terraform.
2. **Migração dos dados já gravados (FEAT-04) é manual, fora deste
   código** — o plano documenta a transformação exata como runbook de
   referência, mas nenhuma ferramenta/endpoint de migração é construído
   nesta feature.
3. **Paginação**: default `limit=20`, máximo `limit=100`.
4. **Precedência de filtros de data**: se `yearMonth` e `dateFrom`/`dateTo`
   vierem juntos, o intervalo de datas (mais específico) governa a
   condição de chave; `yearMonth` não é validado contra o intervalo.
5. **Guard do loop de paginação interna** (necessário porque
   `FilterExpression` de valor pode devolver menos itens que `limit` por
   página do DynamoDB): máximo 25 iterações; ao estourar, retorna erro
   (`Error.Failure` → 500) em vez de página incompleta silenciosa.
6. **Cobertura de teste**: além dos Unit Tests de Handler/Validator e
   Component Tests do endpoint, será criado um teste de unidade dedicado
   para `DynamoDbExpenseRepository.QueryAsync` (mock de `IAmazonDynamoDB`
   via NSubstitute), cobrindo escolha de índice, condição de chave e o
   loop de paginação — é a lógica de maior risco da feature e não é
   exercitada pelos Component Tests (que mockam `IExpenseRepository`
   inteiro).

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Nada |
| Application | Novo `GetExpensesQuery`+Handler+Result; `GetExpensesQueryValidator`; `IExpenseRepository` ganha `QueryAsync`; novos DTOs `ExpenseQueryFilter`/`ExpenseQueryPage`/`ExpenseQueryItem`; `ExpenseCursorCodec` |
| Infrastructure | `DynamoDbExpenseRepository`: altera `SaveAsync` (SK/GSI1SK diários) e implementa `QueryAsync` (índice, condição de chave, filtro de valor, paginação, cursor) |
| Api | `ExpenseEndpoints`: `MapGet("/", GetExpenses)` + `GetExpensesRequest` (`[AsParameters]`) |
| AWS/Terraform | Nenhuma mudança (confirmado em `backend/infra/terraform/dynamodb.tf` — atributos PK/SK/GSI1PK/GSI1SK já existem, só o formato do valor muda) |
| Dados existentes | Runbook de migração manual documentado (seção própria abaixo), execução fora deste código |

## Contratos Application-layer

### `GetExpensesQuery` (novo: `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs`, mirror de `RegisterExpenseCommand.cs` — Query+Handler+Result no mesmo arquivo)

```csharp
public sealed record GetExpensesQuery(
    string UserId,
    string? YearMonth,
    string? Category,
    string? DateFrom,          // string, não DateOnly — validação de formato via FluentValidation, não bind automático
    string? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int? Limit) : IQuery<Result<GetExpensesResult>>;   // Mediator.Abstractions 3.0.2 expõe IQuery/IQueryHandler nativamente (confirmado)

public sealed class GetExpensesQueryHandler : IQueryHandler<GetExpensesQuery, Result<GetExpensesResult>>
{
    private const int DefaultLimit = 20;
    private readonly IExpenseRepository _expenseRepository;

    public GetExpensesQueryHandler(IExpenseRepository expenseRepository) => _expenseRepository = expenseRepository;

    public async ValueTask<Result<GetExpensesResult>> Handle(GetExpensesQuery query, CancellationToken cancellationToken)
    {
        // Enum.Parse/DateOnly.ParseExact aqui não são "validação de negócio" (se o valor chegou aqui, o
        // validator já garantiu formato/consistência) — mesma prática de RegisterExpenseCommandHandler.
        ExpenseCategory? category = query.Category is null
            ? null
            : Enum.Parse<ExpenseCategory>(query.Category, ignoreCase: true);

        var filter = new ExpenseQueryFilter(
            UserId: query.UserId,
            YearMonth: query.YearMonth,
            Category: category,
            DateFrom: query.DateFrom is null ? null : DateOnly.ParseExact(query.DateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTo: query.DateTo is null ? null : DateOnly.ParseExact(query.DateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            MinAmountInCents: query.MinAmountInCents,
            MaxAmountInCents: query.MaxAmountInCents,
            Cursor: query.Cursor,
            Limit: query.Limit ?? DefaultLimit);

        var page = await _expenseRepository.QueryAsync(filter, cancellationToken);
        return Result.Success(GetExpensesResult.FromPage(page));
    }
}
```

### DTOs do repositório (novos arquivos em `GastosApp.Application/Common/Interfaces/` ou `Expenses/Queries/GetExpenses/` — um arquivo por record)

```csharp
public sealed record ExpenseQueryFilter(
    string UserId,
    string? YearMonth,
    ExpenseCategory? Category,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int Limit);

public sealed record ExpenseQueryItem(   // projeção mínima Infra→Application, não é a entidade Expense
    string Id, string Description, long AmountInCents,
    ExpenseCategory Category, DateOnly ExpenseDate, DateTimeOffset CreatedAt);

public sealed record ExpenseQueryPage(IReadOnlyList<ExpenseQueryItem> Items, string? NextCursor);
```

### `IExpenseRepository` (adicionar método)

```csharp
public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
}
```

### `GetExpensesResult`/`ExpenseSummary` (mesmo arquivo do Query — factory method conforme regra da constitution "Result via Factory Method")

```csharp
public sealed record GetExpensesResult(IReadOnlyList<ExpenseSummary> Items, string? NextCursor)
{
    public static GetExpensesResult FromPage(ExpenseQueryPage page) =>
        new(page.Items.Select(ExpenseSummary.FromQueryItem).ToList(), page.NextCursor);
}

public sealed record ExpenseSummary(
    string Id, string Description, long AmountInCents,
    string Category, DateOnly ExpenseDate, DateTimeOffset CreatedAt)
{
    public static ExpenseSummary FromQueryItem(ExpenseQueryItem item) =>
        new(item.Id, item.Description, item.AmountInCents, item.Category.ToString(), item.ExpenseDate, item.CreatedAt);
}
```
Shape casa exatamente com o JSON de resposta 200 do spec.

### `GetExpensesQueryValidator` (mirror de `RegisterExpenseCommandValidator.cs`)

Regras (todas com `.WithMessage(...)` em português, `ClassLevelCascadeMode = CascadeMode.Stop`):
- `YearMonth`: `null` ou regex `^\d{4}-(0[1-9]|1[0-2])$`
- `Category`: `null` ou `Enum.TryParse<ExpenseCategory>(..., ignoreCase: true)` + `Enum.IsDefined`
- `DateFrom`/`DateTo`: `null` ou `DateOnly.TryParseExact(date, "yyyy-MM-dd", ...)`
- Quando ambos presentes e válidos: `DateFrom <= DateTo`
- `MinAmountInCents`/`MaxAmountInCents`: quando presentes, `> 0`
- Quando ambos presentes: `MinAmountInCents <= MaxAmountInCents`
- `Limit`: quando presente, `InclusiveBetween(1, 100)`
- `Cursor`: quando presente, deve decodificar via `ExpenseCursorCodec.TryDecode` (base64 + JSON válidos + shape esperado)

### `ExpenseCursorCodec` (novo, `Application/Common/Cursors/` ou junto de `GetExpenses/` — lógica pura, sem tipos AWS)

```csharp
public sealed record ExpenseCursorPayload(string Index, Dictionary<string, string> LastEvaluatedKey);
// Index: "Base" | "GSI1" — necessário pro repositório saber qual Query (tabela ou GSI1) retomar.
// LastEvaluatedKey: cópia 1:1 dos atributos String do LastEvaluatedKey da SDK (PK/SK e, se GSI1, também GSI1PK/GSI1SK
// — GSI1 tem projection ALL e o LastEvaluatedKey de uma Query em GSI sempre inclui a chave primária da tabela base).

public static class ExpenseCursorCodec
{
    public static string Encode(ExpenseCursorPayload payload); // JSON (System.Text.Json) + Base64Url
    public static bool TryDecode(string cursor, out ExpenseCursorPayload? payload); // try/catch interno, nunca lança
}
```
Reaproveitado tanto pelo Validator (Application) quanto pelo `DynamoDbExpenseRepository` (Infrastructure depende de Application, nunca o contrário — Clean Architecture preservada).

**Decisão registrada (não bloqueante)**: o cursor não é validado quanto à
coerência com os filtros da chamada atual (cliente trocar `category` entre
páginas geraria uma consulta "Frankenstein"). Aceito o risco pela baixa
criticidade (uso pessoal, sem dados sensíveis expostos entre usuários — o
`UserId` sempre vem do JWT, nunca do cursor).

## Infrastructure-layer — `DynamoDbExpenseRepository`

### `SaveAsync` — único diff: granularidade diária

```csharp
var day = expense.ExpenseDate.ToString("yyyy-MM-dd");
["SK"] = new AttributeValue { S = $"TXN#{day}#{expense.Id}" },
["GSI1SK"] = new AttributeValue { S = $"{day}#{expense.Id}" },
```
(`GSI1PK` continua `USER#{userId}#{category}`; `ExpenseDate`/demais atributos inalterados.)

### `QueryAsync` — árvore de decisão

**Índice**: `Category` presente → GSI1 (`GSI1PK = USER#{userId}#{category}`); ausente → tabela base (`PK = USER#{userId}`).

**Condição de chave (SK/GSI1SK)**, por combinação de filtros de data (intervalo de datas sempre prevalece sobre `yearMonth` quando ambos presentes — decisão confirmada):

| Filtros | Condição |
|---|---|
| Nenhum | Só PK/GSI1PK, sem condição de SK |
| Só `yearMonth` | `begins_with(SK, "TXN#{yearMonth}")` (base) / `begins_with(GSI1SK, "{yearMonth}")` (GSI1) |
| Só `dateFrom` | `SK >= "TXN#{dateFrom}"` / `GSI1SK >= "{dateFrom}"` |
| Só `dateTo` | `SK < "TXN#{dateTo.AddDays(1)}"` / `GSI1SK < "{dateTo.AddDays(1)}"` (limite exclusivo — evita sufixo unicode "mágico"; comparação lexicográfica funciona pois formato é `yyyy-MM-dd` zero-padded) |
| `dateFrom` e `dateTo` | `SK >= "TXN#{dateFrom}" AND SK < "TXN#{dateTo.AddDays(1)}"` (duas condições `AND` no `KeyConditionExpression`) |
| `yearMonth` + intervalo de datas juntos | Intervalo de datas governa; `yearMonth` ignorado como condição extra |

`ScanIndexForward = false` (descendente) para já vir ordenado por SK
decrescente nativamente — com SK diário isso corresponde a `expenseDate`
decrescente.

**FilterExpression** (sempre que `MinAmountInCents`/`MaxAmountInCents`
presentes, pois `AmountInCents` nunca está em chave): `AmountInCents >=
:min` e/ou `<= :max`, combinados com `AND`.

### Loop de paginação (preencher `filter.Limit` apesar do FilterExpression)

```
collected = []
exclusiveStartKey = cursor decodificado (ou null)
iterations = 0

repeat:
    iterations += 1
    if iterations > 25: throw / retornar Error.Failure (500)   // guard confirmado
    response = Query(BuildRequest(filter, exclusiveStartKey))  // Limit = filter.Limit, sem overfetch
    collected += response.Items
    exclusiveStartKey = response.LastEvaluatedKey (ou null se vazio)
until collected.Count >= filter.Limit OR exclusiveStartKey is null

page = collected.Take(filter.Limit)
```

**Ponto crítico**: se `collected.Count > filter.Limit` na última iteração,
o `NextCursor` deve ser reconstruído a partir do **último item
efetivamente incluído em `page`** (extraindo `PK`/`SK`/`GSI1PK`/`GSI1SK`
desse item, já presentes nele — GSI1 tem projeção `ALL`), nunca do
`LastEvaluatedKey` bruto da resposta SDK — senão a próxima página pularia
itens. Se `page` esgotou exatamente os dados (`collected.Count <=
filter.Limit` e `exclusiveStartKey is null`), `NextCursor = null`.

### Cursor no repositório

- Entrada: `ExpenseCursorCodec.TryDecode` → `Dictionary<string,string>` →
  `Dictionary<string,AttributeValue>` (`new AttributeValue { S = v }`) como
  `ExclusiveStartKey` da primeira `Query`.
- Saída: monta `ExpenseCursorPayload` com `Index` (derivado no mesmo passo
  desta chamada) + atributos do último item incluído, `Encode`.

## Api-layer — endpoint

```csharp
group.MapGet("/", GetExpenses);   // dentro do MapGroup("/expenses").RequireAuthorization() já existente

public sealed record GetExpensesRequest(
    string? YearMonth, string? Category, string? DateFrom, string? DateTo,
    long? MinAmountInCents, long? MaxAmountInCents, string? Cursor, int? Limit);

private static async Task<IResult> GetExpenses(
    [AsParameters] GetExpensesRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
{
    var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var query = new GetExpensesQuery(userId!, request.YearMonth, request.Category, request.DateFrom,
        request.DateTo, request.MinAmountInCents, request.MaxAmountInCents, request.Cursor, request.Limit);
    var result = await sender.Send(query, cancellationToken);
    return result.ToHttpResult(value => Results.Ok(value));
}
```
`RequireAuthorization()` do grupo já cobre 401 sem token (US10), sem código extra.

## Mapeamento de erros

Todos os erros de validação (formato inválido de `yearMonth`/`category`/
`dateFrom`/`dateTo`, inconsistência `dateFrom>dateTo` ou `min>max`,
`limit` fora de `[1,100]`, `cursor` corrompido) → `Error.Validation`,
código fixo `validation-error` (mesmo padrão do `ValidationBehavior`
atual) → 400, `detail` traz a mensagem específica do campo. Sem token →
401 (middleware de auth, antes do Mediator, já funciona hoje). Estouro do
guard de paginação ou exceção inesperada do DynamoDB → `Error.Failure` /
exceção não capturada → 500 (mesmo comportamento hoje de `SaveAsync`).

## Runbook de migração manual (referência — execução fora deste código)

Formato antigo → novo, por item já persistido (FEAT-04):
- `SK`: `TXN#{yyyy-MM}#{id}` → `TXN#{yyyy-MM-dd}#{id}`
- `GSI1SK`: `{yyyy-MM}#{id}` → `{yyyy-MM-dd}#{id}`
- O `{yyyy-MM-dd}` vem do atributo `ExpenseDate` do próprio item (já
  gravado nesse formato desde a FEAT-04, não muda).
- Nenhum outro atributo muda (`PK`, `GSI1PK`, `Description`,
  `AmountInCents`, `Category`, `ExpenseDate`, `Tipo`, `CreatedAt`
  permanecem idênticos).
- Como `SK`/`GSI1SK` são chave primária/de índice, não são editáveis
  in-place: operação é `PutItem` do item com `SK`/`GSI1SK` novos (demais
  atributos idênticos) seguido de `DeleteItem` do item antigo (mesma `PK`,
  `SK` antigo) — ou `TransactWriteItems` com Put+Delete atômico.
- Enumerar os itens a migrar via `Query` por `PK=USER#<userId>` (userId
  conhecido, informado manualmente) — nunca `Scan`, mesmo sendo operação
  administrativa pontual fora do runtime da aplicação.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/GetExpensesQueryValidatorTests.cs` — todas as regras da
  seção "Validator" acima, incluindo casos de `cursor` corrompido
  (base64 inválido, JSON inválido, shape errado) e "todos os filtros
  ausentes → válido" (US7)
- `Application/GetExpensesQueryHandlerTests.cs` — monta `ExpenseQueryFilter`
  corretamente a partir da Query (incluindo default de `Limit=20` e parse
  de `category`/datas), mapeia `ExpenseQueryPage` → `GetExpensesResult`
  via `FromPage`, `Received(1).QueryAsync(Arg.Is<ExpenseQueryFilter>(...))`
- `Infrastructure/DynamoDbExpenseRepositoryQueryTests.cs` (novo padrão de
  teste no projeto — mock de `IAmazonDynamoDB` via NSubstitute) — decisão
  de índice (GSI1 vs base), construção de `KeyConditionExpression` por
  combinação de filtros de data, `FilterExpression` de valor, loop de
  paginação preenchendo `limit` com múltiplas respostas parciais, guard de
  25 iterações, encode/decode de cursor, reconstrução do `NextCursor` a
  partir do último item incluído (não do LEK bruto)

### Component tests (`backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs`, mockando `IExpenseRepository.QueryAsync`)

Cobrindo os 11 critérios de aceite do spec.md, incluindo (não exaustivo):
sem filtros (US7), `yearMonth` (US1), `category` (US2), `category`+`yearMonth`
(US3), `dateFrom`/`dateTo` (US4), faixa de valor (US5), todos combinados
(US6), paginação com `cursor` em duas chamadas sequenciais (US8),
isolamento entre dois usuários (US9), sem token → 401 sem chamar
repositório (US10), filtros inconsistentes → 400 com `[Theory]`/`[InlineData]`
por caso (US11). Mais: exceção inesperada do repositório → 500; `limit`
acima do máximo → 400; `limit` ausente → default 20 repassado ao filtro.

## Critical Files

- `backend/src/GastosApp.Application/Common/Interfaces/IExpenseRepository.cs` — adicionar `QueryAsync`
- `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQuery.cs` (novo)
- `backend/src/GastosApp.Application/Expenses/Queries/GetExpenses/GetExpensesQueryValidator.cs` (novo)
- `backend/src/GastosApp.Application/Common/Cursors/ExpenseCursorCodec.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Expenses/DynamoDbExpenseRepository.cs` — `SaveAsync` (SK diário) + `QueryAsync` (novo)
- `backend/src/GastosApp.Api/Endpoints/ExpenseEndpoints.cs` — `MapGet("/", GetExpenses)` + `GetExpensesRequest`
- `backend/tests/GastosApp.UnitTests/Application/GetExpensesQueryValidatorTests.cs` (novo)
- `backend/tests/GastosApp.UnitTests/Application/GetExpensesQueryHandlerTests.cs` (novo)
- `backend/tests/GastosApp.UnitTests/Infrastructure/DynamoDbExpenseRepositoryQueryTests.cs` (novo)
- `backend/tests/GastosApp.ComponentTests/Expenses/ExpenseEndpointsTests.cs` — cenários GET adicionados

## Verificação

- `dotnet build backend/GastosApp.sln` — confirma que `IQuery`/`IQueryHandler`
  compilam e o source generator do Mediator descobre o novo handler
- `dotnet test backend/GastosApp.sln` — suíte completa (Unit + Component)
  cobrindo os cenários acima
- Smoke manual (opcional, contra AWS real per `architecture.md`): registrar
  2-3 despesas via `POST /expenses` em meses/categorias diferentes, então
  exercitar `GET /expenses` com cada combinação de filtro e paginação
