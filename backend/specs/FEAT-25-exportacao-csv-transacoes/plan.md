# Plan: FEAT-25 — Exportação CSV de transações — Plano Técnico

## Contexto técnico

`spec.md` fecha: `GET /transactions/export` reaproveitando os mesmos
filtros opcionais e combináveis de `GET /transactions` (`tipo`,
`categoryId`, `yearMonth`, `dateFrom`, `dateTo`, `minAmountInCents`,
`maxAmountInCents`, sem `cursor`/`limit`), retornando sempre o
resultado completo do filtro num único CSV (`Content-Type: text/csv;
charset=utf-8`, UTF-8 com BOM, delimitador `;`, escaping RFC 4180).
Colunas pensadas pra abrir direto numa planilha, não um espelho do JSON
de `GET /transactions`: `data;descricao;categoria;tipo;valor;lancadoPor`
— `categoria` é o **nome** (resolvido a partir do `categoryId`) e
`valor` é reais com vírgula decimal (`45,90`), não centavos. Acesso
liberado a qualquer papel autenticado da conta (sem `RoleEndpointFilters
.Require`), sem 403/404 — filtro sem resultado retorna 200 com CSV só
de cabeçalho.

Mesmo perfil de complexidade das FEAT-23/24: não cria entidade de
Domain, não toca `Transaction`/`Category` existentes, reaproveita 100%
dos métodos de repositório já existentes
(`ITransactionRepository.QueryAsync`, `ICategoryRepository.ListAsync`,
`IMembershipRepository` via `CreatedByLabelResolver`, já usado por
`GetTransactionsQueryHandler`). O trabalho real é um novo módulo
`Transactions/Queries/ExportTransactions/` (Query + Handler + Validator
+ um formatter puro de CSV) e uma nova rota no grupo `/transactions` já
existente.

**Confirmado no `/specify`:** a ideia de expandir pra Excel multi-aba
com categorias/membros foi descartada pelo usuário — este plano cobre
só o escopo original (CSV, só transações). Isso também significa que
**nenhum risco de Native AOT é introduzido aqui**: geração de CSV é
concatenação de string + `Encoding.UTF8`, sem lib externa de
serialização/reflection — diferente do que uma lib de `.xlsx`
(ClosedXML/EPPlus) exigiria.

**Decisões técnicas não óbvias a partir do `spec.md`:**

1. **Novo módulo `ExportTransactions` (Query/Handler/Validator
   próprios), não reaproveita `GetTransactionsQuery` diretamente.**
   Apesar de os filtros serem idênticos, o tipo de retorno é
   completamente diferente (`byte[]` CSV vs `GetTransactionsResult`
   JSON paginado) e a lógica de montagem de linha (nome de categoria,
   formatação de valor em reais, escaping RFC 4180) não faz sentido
   dentro do Handler de `GetTransactions`. Mesmo padrão já usado pelas
   FEAT-23/24: `GetReportsQueryHandler` também tem seu próprio módulo
   mesmo reaproveitando os mesmos filtros/repositórios de
   `GetTransactions`.
2. **`ExportTransactionsQueryValidator` duplica (não reaproveita via
   herança/composição) as regras de `GetTransactionsQueryValidator`
   pros campos em comum.** Decisão deliberada: o projeto já tem esse
   padrão de duplicação pequena e auto-contida entre validators (ex.:
   `BeAValidDate` está duplicado em `GetTransactionsQueryValidator` e
   `GetReportsQueryValidator`, sem abstração comum) — introduzir uma
   base class/interface compartilhada agora tocaria um validator já
   testado (`GetTransactionsQueryValidator`) só pra economizar ~15
   linhas, sem ganho real de manutenção dado o histórico do projeto.
3. **`Limit = int.MaxValue` na única chamada a `QueryAsync`, mesma
   decisão já confirmada nas FEAT-23/24** — evita exportação
   silenciosamente truncada; a salvaguarda de custo continua sendo o
   `MaxPaginationIterations` já existente em
   `DynamoDbTransactionRepository`. Reaproveitada sem nova confirmação
   por já ser precedente aceito no mesmo repositório.
4. **Formatação do CSV isolada num formatter puro
   (`TransactionCsvBuilder`, sem I/O), testável sem mockar
   repositório** — mesmo racional do `PeriodCalculator` da FEAT-24:
   `valor` (centavos → reais com vírgula), escaping RFC 4180 e
   montagem do cabeçalho/linhas são lógica pura, testada isoladamente
   em `UnitTests` sem precisar de `WebApplicationFactory`.
5. **`ICategoryRepository.ListAsync(accountId, tipo: null, ...)`**
   (sem filtro de tipo) — diferente de `GetReportsQueryHandler`, que só
   busca categorias `tipo="despesa"`. Aqui as transações exportadas
   podem ser despesa **ou** receita, então o dicionário de nomes
   precisa cobrir categorias dos dois tipos. `ListAsync` já suporta
   `tipo: null` (retorna todas, ver
   `DynamoDbCategoryRepository.ListAsync`) — nenhuma mudança no
   repositório.
6. **Rota `GET /transactions/export` registrada antes de
   `GET /transactions/{id}`, embora não seja estritamente necessário.**
   O roteamento do ASP.NET Core já prioriza segmentos literais sobre
   parâmetros de rota (`/export` nunca seria capturado por `/{id}`,
   independente da ordem de registro) — a ordem aqui é só por
   legibilidade/defensividade, não por necessidade funcional.
7. **`Results.File(bytes, contentType, fileDownloadName)`** (Minimal
   API nativo) em vez de montar `Content-Disposition` manualmente — já
   seta o header correto (`attachment; filename="transacoes.csv"`) e o
   `Content-Type` informado, sem código extra.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Nenhuma mudança — reaproveita `Transaction`/`Category` como já existem |
| Application | Novo módulo `Transactions/Queries/ExportTransactions/` (`ExportTransactionsQuery` + Handler + `ExportTransactionsQueryValidator` + `TransactionCsvBuilder`, formatter puro); reaproveita `ITransactionRepository.QueryAsync`, `ICategoryRepository.ListAsync`, `IMembershipRepository`/`CreatedByLabelResolver` — nenhuma interface de repositório muda |
| Infrastructure | Nenhuma mudança — nenhum novo método de repositório, nenhum novo atributo/índice DynamoDB |
| Api | `TransactionEndpoints.cs` ganha `GET /export` no grupo `/transactions` já existente (sem `RoleEndpointFilters.Require` — qualquer papel autenticado passa); `AppJsonSerializerContext` ganha `ExportTransactionsRequest` (resposta não é JSON, não entra no contexto) |
| AWS/Terraform | Nenhum recurso novo — mesma tabela `GastosApp`, mesmos `GSI1`/`GSI2`/índice base já provisionados |

## Application-layer

### `TransactionCsvBuilder` (`Transactions/Queries/ExportTransactions/TransactionCsvBuilder.cs`, novo)

```csharp
namespace GastosApp.Application.Transactions.Queries.ExportTransactions;

public sealed record ExportTransactionRow(
    DateOnly Date,
    string Description,
    string CategoryNome,
    string Tipo,
    long AmountInCents,
    string CreatedByLabel);

// Formatter puro (sem I/O) — testável isoladamente, sem mockar repositório.
// Colunas pensadas pra abrir direto numa planilha (spec.md, decisões de
// escopo 2-5): nome de categoria (não id), valor em reais com vírgula
// decimal (não centavos), delimitador ";" (não ",", que já é o separador
// decimal), UTF-8 com BOM, escaping RFC 4180.
public static class TransactionCsvBuilder
{
    private const string Delimiter = ";";
    private const string NewLine = "\r\n";
    private static readonly string[] Header = ["data", "descricao", "categoria", "tipo", "valor", "lancadoPor"];

    public static byte[] Build(IReadOnlyList<ExportTransactionRow> rows)
    {
        var lines = new List<string> { string.Join(Delimiter, Header) };
        lines.AddRange(rows.Select(BuildRow));
        var content = string.Join(NewLine, lines) + NewLine;

        // encoderShouldEmitUTF8Identifier: true -> grava o BOM (EF BB BF) no
        // início do arquivo, necessário pro Excel reconhecer acentuação sem
        // pedir a codificação manualmente (spec.md, decisão de escopo 4).
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content);
    }

    private static string BuildRow(ExportTransactionRow row) => string.Join(Delimiter, new[]
    {
        row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Escape(row.Description),
        Escape(row.CategoryNome),
        row.Tipo,
        FormatValor(row.AmountInCents),
        Escape(row.CreatedByLabel)
    });

    // Única exceção à convenção "sempre centavos" do projeto — este é o único
    // ponto da API pensado pra consumo humano direto (spec.md, decisão de
    // escopo 2). "0.00" (invariant) nunca usa separador de milhar; troca só o
    // separador decimal "." por "," (padrão pt-BR).
    private static string FormatValor(long amountInCents) =>
        (amountInCents / 100m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Escape(string field)
    {
        var needsQuoting = field.Contains(Delimiter, StringComparison.Ordinal)
            || field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r');

        return needsQuoting ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
```

### `ExportTransactionsQuery` (`Transactions/Queries/ExportTransactions/ExportTransactionsQuery.cs`)

```csharp
public sealed record ExportTransactionsQuery(
    string AccountId,
    string CallerUserId,
    string? Tipo,
    string? YearMonth,
    string? CategoryId,
    string? DateFrom,
    string? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents) : IQuery<Result<byte[]>>;

public sealed class ExportTransactionsQueryHandler : IQueryHandler<ExportTransactionsQuery, Result<byte[]>>
{
    // Sem paginação exposta (spec.md, decisão de escopo 1) — mesma decisão já
    // confirmada nas FEAT-23/24 pra "sempre o total, nunca truncado".
    private const int NoTruncationLimit = int.MaxValue;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMembershipRepository _membershipRepository;

    public ExportTransactionsQueryHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IMembershipRepository membershipRepository)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<byte[]>> Handle(ExportTransactionsQuery query, CancellationToken cancellationToken)
    {
        var filter = new TransactionQueryFilter(
            AccountId: query.AccountId,
            Tipo: query.Tipo,
            YearMonth: query.YearMonth,
            CategoryId: query.CategoryId,
            DateFrom: query.DateFrom is null ? null : DateOnly.ParseExact(query.DateFrom, DateFormat, CultureInfo.InvariantCulture),
            DateTo: query.DateTo is null ? null : DateOnly.ParseExact(query.DateTo, DateFormat, CultureInfo.InvariantCulture),
            MinAmountInCents: query.MinAmountInCents,
            MaxAmountInCents: query.MaxAmountInCents,
            Cursor: null,
            Limit: NoTruncationLimit);

        var page = await _transactionRepository.QueryAsync(filter, cancellationToken);

        // tipo: null -> todas as categorias (despesa e receita), diferente do
        // GetReportsQueryHandler (só "despesa") — as transações exportadas
        // podem ser dos dois tipos.
        var categories = await _categoryRepository.ListAsync(query.AccountId, tipo: null, cancellationToken);
        var nomePorCategoria = categories.ToDictionary(c => c.Id, c => c.Nome);

        // Cache por página — mesmo racional do GetTransactionsQueryHandler:
        // evita repetir FindByAccountAndUserIdAsync pro mesmo createdByUserId
        // em toda transação lançada pelo mesmo membro.
        var labelCache = new Dictionary<string, string>();
        var rows = new List<ExportTransactionRow>(page.Items.Count);
        foreach (var item in page.Items)
        {
            if (!labelCache.TryGetValue(item.CreatedByUserId, out var label))
            {
                label = await CreatedByLabelResolver.ResolveAsync(
                    _membershipRepository, query.AccountId, item.CreatedByUserId, query.CallerUserId, cancellationToken);
                labelCache[item.CreatedByUserId] = label;
            }

            rows.Add(new ExportTransactionRow(
                item.Date,
                item.Description,
                // Defesa contra categoria excluída depois de já ter transações
                // lançadas — mesmo fallback já usado em GetReportsQueryHandler.
                nomePorCategoria.GetValueOrDefault(item.CategoryId, item.CategoryId),
                item.Tipo,
                item.AmountInCents,
                label));
        }

        return Result.Success(TransactionCsvBuilder.Build(rows));
    }
}
```

### `ExportTransactionsQueryValidator` (`Transactions/Queries/ExportTransactions/ExportTransactionsQueryValidator.cs`)

```csharp
public sealed partial class ExportTransactionsQueryValidator : AbstractValidator<ExportTransactionsQuery>
{
    private const string DateFormat = "yyyy-MM-dd";

    public ExportTransactionsQueryValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.Tipo)
            .Must(tipo => tipo is null or "despesa" or "receita")
            .WithMessage("tipo deve ser \"despesa\" ou \"receita\".");

        RuleFor(q => q.YearMonth)
            .Must(ym => ym is null || YearMonthRegex().IsMatch(ym))
            .WithMessage("yearMonth deve estar no formato YYYY-MM.");

        RuleFor(q => q.DateFrom)
            .Must(BeAValidDate)
            .WithMessage("dateFrom deve estar no formato YYYY-MM-DD.");

        RuleFor(q => q.DateTo)
            .Must(BeAValidDate)
            .WithMessage("dateTo deve estar no formato YYYY-MM-DD.");

        RuleFor(q => q)
            .Must(HaveConsistentDateRange)
            .WithMessage("dateFrom não pode ser posterior a dateTo.")
            .When(q => BeAValidDate(q.DateFrom) && BeAValidDate(q.DateTo) && q.DateFrom is not null && q.DateTo is not null);

        RuleFor(q => q.MinAmountInCents)
            .GreaterThan(0).WithMessage("minAmountInCents deve ser maior que zero.")
            .When(q => q.MinAmountInCents is not null);

        RuleFor(q => q.MaxAmountInCents)
            .GreaterThan(0).WithMessage("maxAmountInCents deve ser maior que zero.")
            .When(q => q.MaxAmountInCents is not null);

        RuleFor(q => q)
            .Must(q => q.MinAmountInCents!.Value <= q.MaxAmountInCents!.Value)
            .WithMessage("minAmountInCents não pode ser maior que maxAmountInCents.")
            .When(q => q.MinAmountInCents is not null && q.MaxAmountInCents is not null);
    }

    private static bool BeAValidDate(string? date) =>
        date is null || DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool HaveConsistentDateRange(ExportTransactionsQuery query) =>
        DateOnly.ParseExact(query.DateFrom!, DateFormat, CultureInfo.InvariantCulture)
            <= DateOnly.ParseExact(query.DateTo!, DateFormat, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonthRegex();
}
```
Regras idênticas às de `GetTransactionsQueryValidator` pros campos em
comum (sem `Cursor`/`Limit`, que não existem aqui) — ver "Decisões
técnicas", item 2, pra justificativa de não compartilhar via herança.

### `ApplicationServiceCollectionExtensions` — registro

```csharp
services.AddScoped<IValidator<ExportTransactionsQuery>, ExportTransactionsQueryValidator>(); // novo
```
(mantém os registros já existentes — nada é removido.)

## Infrastructure-layer

Nenhuma mudança. `DynamoDbTransactionRepository.QueryAsync` (filtros
`Tipo`/`CategoryId`/`YearMonth`/`DateFrom`/`DateTo`/
`MinAmountInCents`/`MaxAmountInCents`, todos já suportados) e
`DynamoDbCategoryRepository.ListAsync(accountId, tipo: null, ...)` já
cobrem exatamente os dois acessos que o Handler precisa.

## Api-layer

### `TransactionEndpoints.cs` — nova rota no grupo `/transactions` existente

```csharp
group.MapGet("/export", ExportTransactions)
    .Produces(StatusCodes.Status200OK, contentType: "text/csv")
    .ProducesProblem(StatusCodes.Status400BadRequest);

// Registrada logo após GET / e antes de GET /{id} — só por legibilidade
// (ver "Decisões técnicas", item 6: roteamento literal > parametrizado
// independe da ordem de registro).
```

```csharp
private static async Task<IResult> ExportTransactions(
    [AsParameters] ExportTransactionsRequest request,
    CurrentAccountContext currentAccount,
    ISender sender,
    CancellationToken cancellationToken)
{
    var query = new ExportTransactionsQuery(
        currentAccount.AccountId!,
        currentAccount.UserId!,
        NullIfEmpty(request.Tipo),
        NullIfEmpty(request.YearMonth),
        NullIfEmpty(request.CategoryId),
        NullIfEmpty(request.DateFrom),
        NullIfEmpty(request.DateTo),
        request.MinAmountInCents,
        request.MaxAmountInCents);

    var result = await sender.Send(query, cancellationToken);
    return result.ToHttpResult(csv => Results.File(csv, "text/csv; charset=utf-8", "transacoes.csv"));
}

public record ExportTransactionsRequest(
    string Tipo = "",
    string YearMonth = "",
    string CategoryId = "",
    string DateFrom = "",
    string DateTo = "",
    long? MinAmountInCents = null,
    long? MaxAmountInCents = null);
```
Mesmo padrão de `GetTransactions` no mesmo arquivo (`NullIfEmpty` já
existe, reaproveitado). Sem `RoleEndpointFilters.Require` — qualquer
papel autenticado da conta ativa passa (`spec.md`, decisão de escopo 7).
`Results.File` já seta `Content-Disposition: attachment;
filename="transacoes.csv"` e o `Content-Type` informado.

### `AppJsonSerializerContext.cs` — nova entrada

```csharp
[JsonSerializable(typeof(ExportTransactionsRequest))]
```
Só o request entra no contexto (usado pro binding `[AsParameters]`/
schema OpenAPI) — a resposta não é JSON, não tem `JsonSerializable`
correspondente (mesmo caso de qualquer response `text/csv`).

`Program.cs`: nenhuma mudança — a rota é adicionada dentro do
`MapTransactionEndpoints()` já existente, sem novo `Map*Endpoints()`.

## Mapeamento de erros

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `tipo` fora de `despesa`/`receita` | `validation-error` | `Validation` | 400 |
| `yearMonth`/`dateFrom`/`dateTo` fora do formato, ou `dateFrom` posterior a `dateTo` | `validation-error` | `Validation` | 400 |
| `minAmountInCents`/`maxAmountInCents` inválido ou `min > max` | `validation-error` | `Validation` | 400 |

Nenhum `Error` novo — reaproveita `Error.Validation` já existente
(mesmas regras de `GetTransactionsQueryValidator`, ver `spec.md`).
`401` continua vindo de `ResolveAccountEndpointFilter` (sem token/conta
não resolvida), sem passar pelo Handler. Sem `403`/`404`/`422` — o
endpoint é liberado a qualquer papel e sempre retorna 200, mesmo pra
filtro sem resultado (CSV só de cabeçalho, `spec.md` US5).

## Recursos AWS

Nenhum recurso novo. Reaproveita a tabela `GastosApp` e o mesmo access
pattern já usado por `GET /transactions` (índice base ou `GSI1`,
dependendo de `categoryId` estar presente). A consulta de categorias
reaproveita `ListAsync`, já usado por `GET /categories`, `GET /summary`
e `GET /reports`. Sem alteração em `backend/infra/terraform/`.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/Transactions/TransactionCsvBuilderTests.cs` (novo, sem
  mock — formatter puro):
  - `Build` com lista vazia → só a linha de cabeçalho
    (`data;descricao;categoria;tipo;valor;lancadoPor`)
  - `Build` com uma linha → colunas na ordem certa, `data` em
    `YYYY-MM-DD`
  - `valor`: `4590` centavos → `"45,90"`; `100` → `"1,00"`; `500000` →
    `"5000,00"` (sem separador de milhar)
  - `descricao`/`categoria`/`lancadoPor` contendo `;` → campo entre
    aspas duplas
  - campo contendo `"` → aspas internas duplicadas (`""`) e campo entre
    aspas
  - campo contendo `\n`/`\r` → campo entre aspas
  - campo sem nenhum caractere especial → não é envolvido em aspas
  - bytes retornados começam com o BOM UTF-8 (`0xEF 0xBB 0xBF`)
- `Application/Transactions/ExportTransactionsQueryValidatorTests.cs`:
  mesmos casos de `GetTransactionsQueryValidatorTests` pros campos em
  comum (`tipo` inválido, `yearMonth`/`dateFrom`/`dateTo` fora do
  formato, `dateFrom > dateTo`, `minAmountInCents`/`maxAmountInCents`
  inválido ou invertido) — sem os casos de `cursor`/`limit`, que não
  existem aqui
- `Application/Transactions/ExportTransactionsQueryHandlerTests.cs`
  (mock `ITransactionRepository`/`ICategoryRepository`/
  `IMembershipRepository`):
  - filtro passado a `QueryAsync` tem `Cursor=null`, `Limit=int.MaxValue`,
    demais campos repassados de `ExportTransactionsQuery`
  - `categoria` de cada linha resolve o nome a partir do `categoryId`
    (`ICategoryRepository.ListAsync` chamado com `tipo: null`)
  - categoria não encontrada no dicionário → fallback pro próprio
    `categoryId` (defesa contra dado legado)
  - `lancadoPor`: "Você" quando `createdByUserId == CallerUserId`,
    e-mail do membro quando é outro membro, "Ex-membro" quando o
    membership não existe mais (mesmos casos de
    `GetTransactionsQueryHandlerTests`)
  - `IMembershipRepository` chamado no máximo uma vez por
    `createdByUserId` distinto (cache por página)
  - página sem itens → `TransactionCsvBuilder.Build` chamado com lista
    vazia (CSV só de cabeçalho)

### Component tests (`backend/tests/GastosApp.ComponentTests/Transactions/ExportTransactionsEndpointTests.cs`, novo)

Cobre as 11 user stories do `spec.md` fim a fim (mock de
`ITransactionRepository`/`ICategoryRepository`/`IMembershipRepository`
via `WebApplicationFactory`, ver FEAT-03): exportação sem filtro;
filtro por `tipo`; filtro por `categoryId` (nome resolvido); filtro por
período (`yearMonth`); sem resultado (200, CSV só de cabeçalho); filtro
inválido (400); formatação de `valor` em reais com vírgula; escaping de
descrição com `;`/`"`; isolamento entre contas; qualquer papel recebe
200; 401 sem token. Inclui asserção de `Content-Type: text/csv;
charset=utf-8` e `Content-Disposition: attachment;
filename="transacoes.csv"` na resposta.

### Teste de regressão já existente

`ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownTen`
precisa ser atualizado pra `...BeyondTheKnownEleven`, incluindo
`ExportTransactionsQueryValidator` na lista fechada de validators
esperados — mesma manutenção já feita nas FEAT-23/24.

## Critical Files

- `backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/TransactionCsvBuilder.cs` (novo)
- `backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/ExportTransactionsQuery.cs` (novo)
- `backend/src/GastosApp.Application/Transactions/Queries/ExportTransactions/ExportTransactionsQueryValidator.cs` (novo)
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Api/Endpoints/TransactionEndpoints.cs`
- `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`
- `backend/tests/GastosApp.UnitTests/DependencyInjection/ApplicationExtensionsTests.cs` — `...BeyondTheKnownEleven`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Transactions`/`Categories`/`Members`/`Summary`/`Reports`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar só a
  adição de `GET /transactions/export` (parâmetros, resposta `200` com
  `text/csv`, `400`/`401`), sem tocar as demais rotas de `/transactions`
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/LocalStack): lançar transações de despesa e receita
  em categorias diferentes, com uma descrição contendo `;` e outra
  contendo `"`; chamar `GET /transactions/export` sem filtro e abrir o
  CSV baixado no Excel/LibreOffice, conferindo acentuação, colunas
  separadas corretamente e o valor com vírgula decimal; repetir com
  `?tipo=`/`?categoryId=`/`?yearMonth=`; tentar filtro sem nenhum
  resultado (CSV só de cabeçalho); tentar `?tipo=invalido` (400)

## Decisões técnicas

1. **Novo módulo `ExportTransactions`, não reaproveita
   `GetTransactionsQuery`** — ver "Contexto técnico", decisão 1. Tipo
   de retorno e lógica de montagem de linha são incompatíveis com o
   Handler existente.
2. **Validator duplicado (não compartilhado via herança) com
   `GetTransactionsQueryValidator`** — ver "Contexto técnico", decisão
   2. Consistente com o padrão já existente de validators pequenos e
   auto-contidos no projeto.
3. **`Limit = int.MaxValue`, mesma decisão já confirmada nas FEAT-23/24**
   — reaproveitada sem nova confirmação por já ser precedente aceito no
   mesmo repositório.
4. **`TransactionCsvBuilder` como formatter puro, sem I/O** — mesmo
   racional do `PeriodCalculator` (FEAT-24): lógica de formatação
   testável isoladamente, sem mockar repositório.
5. **`ICategoryRepository.ListAsync(accountId, tipo: null, ...)`** —
   diferente do `GetReportsQueryHandler` (só `"despesa"`), porque a
   exportação cobre despesa e receita.
6. **Sem `RoleEndpointFilters.Require` no `GET /export`** — mesmo
   padrão de `GET /transactions`/`GET /reports`/`GET /summary`
   (`spec.md`, decisão de escopo 7 / US10).
7. **`Results.File` (Minimal API nativo) em vez de montar
   `Content-Disposition` manualmente** — já cobre header e
   `Content-Type` corretamente.

## Pontos que precisam confirmação do usuário antes do `/tasks`

1. **Formatter puro `TransactionCsvBuilder` vs lógica direto no
   Handler.** O plano extrai a formatação em uma classe estática
   separada, testável sem mock — se preferir manter tudo no Handler
   (mais raso, menos um arquivo), é só avisar antes do `/tasks`.
2. **Não compartilhar regras de validação com
   `GetTransactionsQueryValidator`** (item 2 acima). Se preferir
   extrair uma base comum agora (em vez de manter o padrão de
   duplicação já usado no projeto), também é só avisar — envolve tocar
   `GetTransactionsQueryValidator`, hoje já testado e em produção.

Fora esses dois pontos, o plano segue 100% os precedentes já
estabelecidos nas FEAT-22/23/24 (mesmos repositórios, mesmo padrão de
Handler/Validator, mesma decisão de `Limit=int.MaxValue`) — nenhum
recurso AWS novo, nenhuma mudança de Domain/Infrastructure.
