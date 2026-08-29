# Plan: FEAT-24 — Relatórios por período — Plano Técnico

## Contexto técnico

`spec.md` fecha: `GET /reports?period=week|month|year&date=YYYY-MM-DD`
(ambos obrigatórios), acessível a qualquer papel autenticado da conta;
calcula início/fim do período a partir de `date` (semana ISO segunda-
domingo, mês calendário ou ano calendário), agrega despesas do período
(`totalCents`, `porCategoria` só com gasto > 0, ordenado decrescente),
compara com o período imediatamente anterior de mesma duração
(`variacaoPercentual`, com regra de `null`/`0` pra divisão por zero) e
aponta a categoria de maior gasto (`maiorGasto`, com
`percentualOrcamento` sobre o orçamento da própria categoria, `null`
sem orçamento definido). Sem tabela agregada nem Streams (decisão já
fechada no roadmap) — tudo calculado via `Query` + agregação em
memória, na própria request.

Mesmo perfil de complexidade da FEAT-23: feature nova mas estrutural
mente simples — não cria entidade de Domain, não toca `Transaction`/
`Category` existentes, reaproveita 100% dos métodos de repositório já
existentes. O trabalho real é um novo módulo `Reports` (Query +
Handler) que calcula duas janelas de data (período atual e anterior) e
faz duas consultas (`ITransactionRepository.QueryAsync` com
`Tipo="despesa"` e `DateFrom`/`DateTo`) + uma consulta de categorias
(`ICategoryRepository.ListAsync(accountId, "despesa")`), agregando os
resultados em memória.

**Decisões técnicas não óbvias a partir do `spec.md`:**

1. **`DateFrom`/`DateTo` em vez de `YearMonth` no `TransactionQueryFilter`.**
   `TransactionQueryFilter` já suporta um intervalo de datas arbitrário
   (`DateFrom`/`DateTo`, usado por `GET /transactions`) — cobre os três
   `period` (semana, mês, ano) com o mesmo mecanismo, sem precisar do
   `YearMonth` (que só cobre mês). `DynamoDbTransactionRepository`
   already resolve `BETWEEN` inclusivo nos dois limites (ver
   `BuildSkCondition`) — nenhuma mudança no repositório.
2. **Duas queries independentes (período atual + período anterior),
   cada uma já filtrando `Tipo="despesa"` na query (`FilterExpression`
   do DynamoDB), em vez de uma query ampla + filtro em memória (como a
   FEAT-23 fez para `receitasCents`/`gastoCents` juntos).** Este
   endpoint não precisa de receitas (`spec.md`, decisão de escopo 4),
   então filtrar `Tipo` já na query evita trazer registros de receita
   que seriam descartados em memória — mais barato em RCU e mais simples
   no Handler (sem `if (item.Tipo == "receita") continue;`).
3. **`Limit = int.MaxValue` nas duas queries, mesma decisão já tomada e
   confirmada com o usuário na FEAT-23** (ver
   `backend/specs/FEAT-23-resumo-mensal-dashboard/plan.md`, "Decisões
   confirmadas com o usuário") — evita agregação silenciosamente
   incompleta; a salvaguarda de custo continua sendo o
   `MaxPaginationIterations` já existente em
   `DynamoDbTransactionRepository`. Reaproveitada aqui sem nova
   confirmação por já ser precedente aceito no mesmo repositório.
4. **Cálculo de datas em `PeriodCalculator` (`Application/Reports/`,
   `static class`), sem dependência de `DateTime.Now`/relógio do
   sistema.** Toda entrada de data vem do parâmetro `date` da query
   (já validado pelo `Validator`) — o cálculo de início/fim do período
   atual e do período anterior é uma função pura
   `(DateOnly date, string period) → (PeriodRange atual, PeriodRange
   anterior)`, sem I/O nem estado, o que torna o Handler 100%
   testável sem mockar relógio (não existe abstração de clock no
   projeto hoje — não é necessária aqui, já que `date` nunca tem
   default implícito, conforme `spec.md` decisão de escopo 1).
5. **Semana ISO calculada via `ISOWeek` (`System.Globalization`, .NET
   nativo)** para achar a segunda-feira da semana que contém `date`:
   `date.AddDays(-(int)date.DayOfWeek == 0 ? -6 : 1 - (int)date.DayOfWeek)`
   é frágil pra `DayOfWeek.Sunday` (valor `0`); em vez disso, usar
   `ISOWeek.GetWeekOfYear`/`ISOWeek.ToDateOnly(ISOWeek.GetYear(date),
   ISOWeek.GetWeekOfYear(date), DayOfWeek.Monday)` — ganha o primeiro
   dia (segunda) da semana ISO diretamente, sem aritmética manual de
   `DayOfWeek`. Fim de semana = início + 6 dias (domingo).

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Nenhuma mudança — reaproveita `Transaction`/`Category` como já existem |
| Application | Novo módulo `Reports/Queries/GetReports/` (Query + Handler + Validator + Results) e `Reports/PeriodCalculator.cs` (função pura de cálculo de datas); reaproveita `ITransactionRepository.QueryAsync`, `ICategoryRepository.ListAsync` — nenhuma interface de repositório muda |
| Infrastructure | Nenhuma mudança — nenhum novo método de repositório, nenhum novo atributo/índice DynamoDB |
| Api | Novo `Endpoints/ReportEndpoints.cs` (grupo `/reports`, só `GET`, sem `RoleEndpointFilters.Require` — qualquer papel autenticado passa); `Program.cs` ganha `app.MapReportEndpoints()`; `AppJsonSerializerContext` ganha as novas entradas |
| AWS/Terraform | Nenhum recurso novo — mesma tabela `GastosApp`, mesmos `GSI1`/`GSI2`/índice base já provisionados |

## Application-layer

### `PeriodCalculator` (`Reports/PeriodCalculator.cs`, novo)

```csharp
namespace GastosApp.Application.Reports;

public readonly record struct PeriodRange(DateOnly Start, DateOnly End);

public static class PeriodCalculator
{
    // Função pura — toda entrada vem do parâmetro `date` já validado pelo
    // Validator (spec.md, decisão de escopo 1: sem default de "hoje").
    public static (PeriodRange Current, PeriodRange Previous) Calculate(DateOnly date, string period) =>
        period switch
        {
            "week" => CalculateWeek(date),
            "month" => CalculateMonth(date),
            "year" => CalculateYear(date),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "period deve ser week, month ou year.")
        };

    private static (PeriodRange, PeriodRange) CalculateWeek(DateOnly date)
    {
        var isoYear = ISOWeek.GetYear(date);
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        var monday = ISOWeek.ToDateOnly(isoYear, isoWeek, DayOfWeek.Monday);
        var current = new PeriodRange(monday, monday.AddDays(6));
        var previous = new PeriodRange(monday.AddDays(-7), monday.AddDays(-1));
        return (current, previous);
    }

    private static (PeriodRange, PeriodRange) CalculateMonth(DateOnly date)
    {
        var firstDay = new DateOnly(date.Year, date.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var current = new PeriodRange(firstDay, lastDay);
        var previousFirstDay = firstDay.AddMonths(-1);
        var previous = new PeriodRange(previousFirstDay, firstDay.AddDays(-1));
        return (current, previous);
    }

    private static (PeriodRange, PeriodRange) CalculateYear(DateOnly date)
    {
        var current = new PeriodRange(new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31));
        var previous = new PeriodRange(new DateOnly(date.Year - 1, 1, 1), new DateOnly(date.Year - 1, 12, 31));
        return (current, previous);
    }
}
```
`period` já chega validado pelo `Validator` (só `week`/`month`/`year`
passam) — o `_ => throw` no `switch` é defesa interna, nunca alcançado
em produção (mesmo padrão de "unreachable guarded by validation" já
implícito em outros Handlers do projeto que confiam no
`ValidationBehavior` rodar antes).

### `GetReportsQuery` (`Reports/Queries/GetReports/GetReportsQuery.cs`)

```csharp
public sealed record GetReportsQuery(
    string AccountId,
    string Period,
    string Date) : IQuery<Result<GetReportsResult>>;

public sealed class GetReportsQueryHandler : IQueryHandler<GetReportsQuery, Result<GetReportsResult>>
{
    // Sem cap de negócio — mesma decisão já confirmada com o usuário na FEAT-23
    // (ver plan.md da FEAT-23, "Decisões confirmadas com o usuário").
    private const int NoTruncationLimit = int.MaxValue;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;

    public GetReportsQueryHandler(ITransactionRepository transactionRepository, ICategoryRepository categoryRepository)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<GetReportsResult>> Handle(GetReportsQuery query, CancellationToken cancellationToken)
    {
        var date = DateOnly.ParseExact(query.Date, DateFormat, CultureInfo.InvariantCulture);
        var (current, previous) = PeriodCalculator.Calculate(date, query.Period);

        var currentTotal = await SumDespesasAsync(query.AccountId, current, cancellationToken);
        var previousPage = await _transactionRepository.QueryAsync(
            BuildFilter(query.AccountId, previous), cancellationToken);
        var previousTotalCents = previousPage.Items.Sum(i => i.AmountInCents);

        var categories = await _categoryRepository.ListAsync(query.AccountId, "despesa", cancellationToken);
        var orcamentoPorCategoria = categories
            .Where(c => c.OrcamentoMensalCents is not null)
            .ToDictionary(c => c.Id, c => c.OrcamentoMensalCents!.Value);
        var nomePorCategoria = categories.ToDictionary(c => c.Id, c => c.Nome);

        var porCategoria = currentTotal.GastoPorCategoria
            .Where(kv => kv.Value > 0)
            .Select(kv => new ReportCategoryItem(kv.Key, nomePorCategoria.GetValueOrDefault(kv.Key, kv.Key), kv.Value))
            .OrderByDescending(c => c.GastoCents)
            .ToList();

        var maiorGasto = porCategoria.Count == 0
            ? null
            : porCategoria[0] with { }; // primeiro item já é o de maior gasto (lista ordenada)

        var maiorGastoResult = maiorGasto is null
            ? null
            : new ReportTopCategory(
                maiorGasto.CategoryId,
                maiorGasto.Nome,
                maiorGasto.GastoCents,
                orcamentoPorCategoria.TryGetValue(maiorGasto.CategoryId, out var orcamento)
                    ? Math.Round((decimal)maiorGasto.GastoCents / orcamento * 100, 1)
                    : null);

        decimal? variacaoPercentual = previousTotalCents == 0
            ? (currentTotal.TotalCents == 0 ? 0m : null)
            : Math.Round((decimal)(currentTotal.TotalCents - previousTotalCents) / previousTotalCents * 100, 1);

        return Result.Success(new GetReportsResult(
            query.Period,
            current.Start,
            current.End,
            currentTotal.TotalCents,
            variacaoPercentual,
            porCategoria,
            maiorGastoResult));
    }

    private async Task<(long TotalCents, Dictionary<string, long> GastoPorCategoria)> SumDespesasAsync(
        string accountId, PeriodRange range, CancellationToken cancellationToken)
    {
        var page = await _transactionRepository.QueryAsync(BuildFilter(accountId, range), cancellationToken);
        var gastoPorCategoria = new Dictionary<string, long>();
        long total = 0;
        foreach (var item in page.Items)
        {
            total += item.AmountInCents;
            gastoPorCategoria[item.CategoryId] = gastoPorCategoria.GetValueOrDefault(item.CategoryId) + item.AmountInCents;
        }

        return (total, gastoPorCategoria);
    }

    private static TransactionQueryFilter BuildFilter(string accountId, PeriodRange range) => new(
        AccountId: accountId,
        Tipo: "despesa",
        YearMonth: null,
        CategoryId: null,
        DateFrom: range.Start,
        DateTo: range.End,
        MinAmountInCents: null,
        MaxAmountInCents: null,
        Cursor: null,
        Limit: NoTruncationLimit);
}

public sealed record GetReportsResult(
    string Period,
    DateOnly StartDate,
    DateOnly EndDate,
    long TotalCents,
    decimal? VariacaoPercentual,
    IReadOnlyList<ReportCategoryItem> PorCategoria,
    ReportTopCategory? MaiorGasto);

public sealed record ReportCategoryItem(string CategoryId, string Nome, long GastoCents);

public sealed record ReportTopCategory(string CategoryId, string Nome, long GastoCents, decimal? PercentualOrcamento);
```

Duas queries (período atual, período anterior) em vez de uma só — o
`TransactionQueryFilter` não suporta duas janelas de data numa
`Query` só (é um único `BETWEEN` na `SK`), e as duas janelas nem são
contíguas o bastante pra valer a pena uma única `Query` mais ampla com
filtro em memória (ex.: `period=year` teria que buscar 2 anos inteiros
pra descartar metade em memória). `previousPage` só precisa do total
(soma), não do agrupamento por categoria — por isso usa `Sum` direto em
vez de `SumDespesasAsync`.

`nomePorCategoria.GetValueOrDefault(kv.Key, kv.Key)` é defesa contra uma
categoria excluída depois de já ter transações lançadas (o
`ExistsByCategoryAsync` hoje bloqueia isso na exclusão — ver
`DynamoDbCategoryRepository` — mas o Handler não deve quebrar se esse
invariante já tiver sido violado por dado legado).

### `GetReportsQueryValidator` (`Reports/Queries/GetReports/GetReportsQueryValidator.cs`)

```csharp
public sealed partial class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    private const string DateFormat = "yyyy-MM-dd";

    public GetReportsQueryValidator()
    {
        RuleFor(q => q.Period)
            .Must(p => p is "week" or "month" or "year")
            .WithMessage("O parâmetro period é obrigatório e deve ser week, month ou year.");

        RuleFor(q => q.Date)
            .NotEmpty().WithMessage("O parâmetro date é obrigatório.")
            .Must(BeAValidDate).WithMessage("date deve estar no formato YYYY-MM-DD.");
    }

    private static bool BeAValidDate(string date) =>
        DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
```
`Period` sem `.NotEmpty()` separado — `Must(p => p is "week" or "month"
or "year")` já rejeita `null`/vazio/qualquer outro valor numa regra só
(mais simples que a validação de `Month` da FEAT-23, que precisava de
regex). `Date` não usa `.When()` — mesmo bug já corrigido na FEAT-23
(`GetSummaryQueryValidator`): `.When()` encadeado no fim da regra
aplica a condição a **toda a cadeia anterior**, inclusive `NotEmpty()`,
fazendo string vazia passar. `Must(BeAValidDate)` já rejeita vazio
sozinho, então nenhum `.When()` é necessário.

### `ApplicationServiceCollectionExtensions` — registro

```csharp
services.AddScoped<IValidator<GetReportsQuery>, GetReportsQueryValidator>(); // novo
```
(mantém os registros já existentes — nada é removido.)

## Infrastructure-layer

Nenhuma mudança. `DynamoDbTransactionRepository.QueryAsync` (com
`DateFrom`/`DateTo`, já suportado) e `DynamoDbCategoryRepository.
ListAsync` já cobrem exatamente os dois acessos que o Handler precisa.

## Api-layer

### `ReportEndpoints` (`Endpoints/ReportEndpoints.cs`, novo)

```csharp
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sem RoleEndpointFilters.Require — qualquer papel autenticado da conta
        // ativa pode consultar (spec.md, decisão de escopo 8).
        group.MapGet("/", GetReports)
            .Produces<GetReportsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetReports(
        [AsParameters] GetReportsRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetReportsQuery(currentAccount.AccountId!, request.Period, request.Date);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}

public record GetReportsRequest(string Period = "", string Date = "");
```
Mesmo padrão de grupo já usado por `SummaryEndpoints`/
`TransactionEndpoints` (`RequireAuthorization()` +
`ResolveAccountEndpointFilter`). Diferente de `SummaryEndpoints`, não
precisa de `CallerUserId` (o Handler não resolve `createdByLabel` nem
nada específico de autor).

`Program.cs`: adicionar `app.MapReportEndpoints();` junto às demais
chamadas.

### `AppJsonSerializerContext.cs` — novas entradas

```csharp
[JsonSerializable(typeof(GetReportsResult))]
[JsonSerializable(typeof(ReportCategoryItem))]
[JsonSerializable(typeof(ReportTopCategory))]
[JsonSerializable(typeof(GetReportsRequest))]
```

## Mapeamento de erros

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `period` ausente ou fora de `week`/`month`/`year` | `validation-error` | `Validation` | 400 |
| `date` ausente, vazia ou fora do formato `YYYY-MM-DD` (inclui data de calendário inválida) | `validation-error` | `Validation` | 400 |

Nenhum `Error` novo — reaproveita `Error.Validation` já existente.
`401` continua vindo de `ResolveAccountEndpointFilter` (sem token/conta
não resolvida), sem passar pelo `Handler`. Sem `403`/`404`/`422` — o
endpoint é liberado a qualquer papel e sempre retorna 200 (mesmo para
período sem dados, `spec.md` US9).

## Recursos AWS

Nenhum recurso novo. Reaproveita a tabela `GastosApp` e o índice base
(`PK`/`SK`, `DateFrom`/`DateTo` via `BETWEEN` na `SK`) já usado por
`GET /transactions` — mesmo access pattern, só com uma janela de datas
diferente por request (semana/mês/ano em vez do intervalo livre que o
frontend de transações escolhe). A consulta de categorias reaproveita
`ListAsync`, já usado por `GET /categories` e `GET /summary`. Sem
alteração em `backend/infra/terraform/`.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/PeriodCalculatorTests.cs` (novo, sem mock — função pura):
  - `period=week`, `date` numa quarta-feira → `Current` = segunda a
    domingo daquela semana ISO; `Previous` = semana ISO anterior
  - `period=week`, `date` num domingo → ainda resolve pra segunda daquela
    mesma semana (não pula pra semana seguinte) — caso de borda de
    `DayOfWeek.Sunday`
  - `period=month`, `date` em qualquer dia do mês → `Current` = primeiro
    ao último dia do mês; `Previous` = mês calendário anterior
    (incluindo troca de ano: `date=2026-01-15` → anterior = dez/2025)
  - `period=year` → `Current` = 01/01 a 31/12 do ano de `date`;
    `Previous` = ano anterior completo
- `Application/GetReportsQueryValidatorTests.cs`: `period` ausente/
  vazio/valor fora de `week|month|year` → inválido; `date` ausente/
  vazia/fora do formato (`2026/08/15`, `agosto`) → inválido; data de
  calendário inválida (`2026-02-30`) → inválido; combinação válida →
  válido
- `Application/GetReportsQueryHandlerTests.cs` (mock
  `ITransactionRepository`/`ICategoryRepository`):
  - `totalCents` soma corretamente as despesas retornadas pro filtro do
    período atual
  - `porCategoria` inclui só categorias com `gastoCents > 0` (categoria
    com transação mas soma zero — caso não ocorre na prática, mas
    categoria sem nenhuma transação no período não aparece), ordenada
    decrescente
  - `maiorGasto` aponta pra categoria do topo de `porCategoria`, com
    `percentualOrcamento` calculado quando a categoria tem
    `orcamentoMensalCents`, `null` quando não tem
  - `porCategoria=[]` e período sem nenhuma despesa → `maiorGasto=null`
  - `variacaoPercentual` positivo quando total atual > total anterior
  - `variacaoPercentual` negativo quando total atual < total anterior
  - `variacaoPercentual=null` quando total anterior = 0 e total atual > 0
  - `variacaoPercentual=0` quando os dois totais são 0
  - filtro passado ao mock em cada uma das duas chamadas tem
    `Tipo="despesa"`, `DateFrom`/`DateTo` corretos por período,
    `Limit=int.MaxValue`

### Component tests (`backend/tests/GastosApp.ComponentTests/Reports/ReportEndpointsTests.cs`, novo)

Cobre as 15 user stories do `spec.md` fim a fim (mock de
`ITransactionRepository`/`ICategoryRepository` via
`WebApplicationFactory`, ver FEAT-03): relatório mensal com dados
batendo com o cenário do mockup; relatório semanal (semana ISO);
relatório anual; `period` ausente/inválido → 400; `date` ausente/
inválida → 400; variação positiva/negativa; variação `null` (período
anterior zerado); ambos os períodos zerados (200 com tudo zerado);
maior gasto com e sem orçamento definido; `porCategoria` ordenada sem
categorias zeradas; isolamento entre contas; qualquer papel recebe 200;
401 sem token.

### Teste de regressão já existente

`ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownNine`
(FEAT-03/FEAT-23) precisa ser atualizado pra `...BeyondTheKnownTen`,
incluindo `GetReportsQueryValidator` na lista fechada de validators
esperados — mesma manutenção já feita na FEAT-23 ao adicionar
`GetSummaryQueryValidator`.

## Critical Files

- `backend/src/GastosApp.Application/Reports/PeriodCalculator.cs` (novo)
- `backend/src/GastosApp.Application/Reports/Queries/GetReports/GetReportsQuery.cs` (novo)
- `backend/src/GastosApp.Application/Reports/Queries/GetReports/GetReportsQueryValidator.cs` (novo)
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Api/Endpoints/ReportEndpoints.cs` (novo)
- `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`
- `backend/src/GastosApp.Api/Program.cs` — `MapReportEndpoints()`
- `backend/tests/GastosApp.UnitTests/DependencyInjection/ApplicationExtensionsTests.cs` — `...BeyondTheKnownTen`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Transactions`/`Categories`/`Members`/`Summary`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar só a
  adição do novo `/reports` (`GET`, parâmetros `period`/`date`, schemas
  de response, `400`/`401`), sem tocar `/transactions`, `/categories`,
  `/members` ou `/summary`
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/LocalStack): registrar despesas em categorias
  diferentes cobrindo semana/mês/ano atual e o período anterior
  correspondente, definir orçamento numa categoria, consultar
  `GET /reports?period=month&date=YYYY-MM-DD` e conferir os números
  batendo manualmente com o que foi lançado; repetir pra `week`/`year`;
  período sem nenhum dado (200 zerado); tentar sem `period`/`date` (400)

## Decisões técnicas

1. **`DateFrom`/`DateTo` em vez de `YearMonth`** — ver "Contexto
   técnico", decisão 1. Cobre os três granularidades com o mesmo
   mecanismo já suportado pelo repositório, sem mudança nele.
2. **Duas queries independentes, cada uma já filtrando `Tipo="despesa"`
   na própria `Query`** — ver "Contexto técnico", decisão 2. Mais barato
   e mais simples que uma query ampla + filtro em memória.
3. **`Limit = int.MaxValue`, mesma decisão já confirmada na FEAT-23** —
   reaproveitada sem nova confirmação por já ser precedente aceito no
   mesmo repositório (`MaxPaginationIterations` continua sendo a
   salvaguarda de custo).
4. **`PeriodCalculator` como função pura, sem abstração de clock** — o
   projeto não tem `IDateTimeProvider`/`IClock` hoje; não é necessário
   introduzir um aqui porque `date` nunca depende implicitamente do
   relógio do servidor (`spec.md`, decisão de escopo 1 — mesma
   filosofia do `month` obrigatório em `/summary`).
5. **Semana ISO via `System.Globalization.ISOWeek`** (.NET nativo) em
   vez de aritmética manual sobre `DayOfWeek` — evita o caso de borda
   de `DayOfWeek.Sunday = 0` quebrar o cálculo da segunda-feira da
   semana.
6. **Sem `RoleEndpointFilters.Require` no grupo `/reports`** — mesmo
   padrão de `GET /summary`/`GET /transactions`/`GET /categories`
   (`spec.md`, decisão de escopo 8 / US14).

## Decisões confirmadas com o usuário (revisão pós-plan)

1. **`decimal` (não `double`) para `variacaoPercentual`/
   `percentualOrcamento`, arredondado a 1 casa decimal** — confirmado.
2. **Duas chamadas a `ITransactionRepository.QueryAsync` por request**
   (período atual + período anterior) em vez de uma única — confirmado,
   aceitável no volume pessoal previsto pelo projeto.
