# Plan: FEAT-23 — Resumo mensal (dashboard) — Plano Técnico

## Contexto técnico

`spec.md` fecha: `GET /summary?month=YYYY-MM` (obrigatório), acessível a
qualquer papel autenticado da conta; agrega transações do mês (`saldo`,
`receitas`, `gasto`) e categorias de despesa com orçamento definido
(`orcamentoTotal`, `porCategoria`, ordenado por gasto decrescente,
incluindo categorias com gasto zero); `ultimosLancamentos` traz as 5
transações mais recentes do mês, no mesmo shape de `GET /transactions`.
Sem tabela agregada nem Streams (decisão já fechada no roadmap) — tudo
calculado via `Query` + agregação em memória, na própria request.

Diferente da FEAT-22 (rename em cascata), esta feature é **inteiramente
nova mas de baixa complexidade estrutural**: não cria nenhuma entidade
de Domain, não toca `Transaction`/`Category` existentes, e reaproveita
100% dos métodos de repositório já existentes (`ITransactionRepository.
QueryAsync`, `ICategoryRepository.ListAsync`) — nenhuma interface de
repositório muda. O trabalho real é um novo módulo `Summary` (Query +
Handler) que orquestra duas consultas já existentes e agrega os
resultados em memória.

**Uma decisão técnica não óbvia a partir do `spec.md`:**

1. **Buscar TODAS as transações do mês, sem cap de paginação de
   negócio.** `ITransactionRepository.QueryAsync` (usado por `GET
   /transactions`) recebe um `Limit` porque a listagem pagina de
   propósito (o cliente pede mais via `cursor`). O resumo não pode
   fazer isso — paginar a agregação produziria totais **incorretos e
   sem nenhum sinal de erro** (ex.: `gastoCents` refletindo só as 20
   primeiras transações do mês). A solução é passar
   `TransactionQueryFilter.Limit = int.MaxValue`: a implementação
   (`DynamoDbTransactionRepository.QueryAsync`) já para naturalmente
   quando `exclusiveStartKey is null` (fim real dos dados), então na
   prática isso busca o mês inteiro. A única salvaguarda contra custo
   descontrolado é o `MaxPaginationIterations = 25` **já existente** no
   repositório (25 páginas de `Query` do DynamoDB) — se uma conta
   algum dia acumular transações suficientes num único mês pra estourar
   25 páginas, a chamada lança `InvalidOperationException` (500) em vez
   de devolver um resumo silenciosamente errado. Falhar alto é
   preferível a um dashboard financeiro com números errados. Nenhuma
   mudança na interface `ITransactionRepository` nem na implementação
   — só o valor passado no `Limit` do filtro, do lado do Handler.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Nenhuma mudança — reaproveita `Transaction`/`Category` como já existem |
| Application | Novo módulo `Summary/Queries/GetSummary/` (Query + Handler + Validator + Results); reaproveita `ITransactionRepository.QueryAsync`, `ICategoryRepository.ListAsync`, `IMembershipRepository`, `CreatedByLabelResolver` e `TransactionSummary` (de `Transactions.Queries.GetTransactions`) — nenhuma interface de repositório muda |
| Infrastructure | Nenhuma mudança — nenhum novo método de repositório, nenhum novo atributo/índice DynamoDB |
| Api | Novo `Endpoints/SummaryEndpoints.cs` (grupo `/summary`, só `GET`, sem `RoleEndpointFilters.Require` — qualquer papel autenticado passa); `Program.cs` ganha `app.MapSummaryEndpoints()`; `AppJsonSerializerContext` ganha as novas entradas |
| AWS/Terraform | Nenhum recurso novo — mesma tabela `GastosApp`, mesmos `GSI1`/`GSI2`/índice base já provisionados |

## Application-layer

### `GetSummaryQuery` (`Summary/Queries/GetSummary/GetSummaryQuery.cs`)

```csharp
public sealed record GetSummaryQuery(
    string AccountId,
    string CallerUserId,
    string Month) : IQuery<Result<GetSummaryResult>>;

public sealed class GetSummaryQueryHandler : IQueryHandler<GetSummaryQuery, Result<GetSummaryResult>>
{
    // Sem cap de negócio — ver "Contexto técnico", decisão 1. A única
    // salvaguarda é o MaxPaginationIterations já existente dentro de
    // ITransactionRepository.QueryAsync.
    private const int NoTruncationLimit = int.MaxValue;
    private const int RecentTransactionsCount = 5;

    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMembershipRepository _membershipRepository;

    public GetSummaryQueryHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IMembershipRepository membershipRepository)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<GetSummaryResult>> Handle(GetSummaryQuery query, CancellationToken cancellationToken)
    {
        // Base index (PK=ACCOUNT#accountId, SK begins_with "TXN#{month}") — mesmo
        // access pattern AP1 de backend/docs/architecture.md, sem CategoryId no
        // filtro (índice base, não GSI1) e sem filtro de Tipo (precisa dos dois).
        var filter = new TransactionQueryFilter(
            AccountId: query.AccountId,
            Tipo: null,
            YearMonth: query.Month,
            CategoryId: null,
            DateFrom: null,
            DateTo: null,
            MinAmountInCents: null,
            MaxAmountInCents: null,
            Cursor: null,
            Limit: NoTruncationLimit);

        var page = await _transactionRepository.QueryAsync(filter, cancellationToken);

        long receitasCents = 0;
        long gastoCents = 0;
        var gastoPorCategoria = new Dictionary<string, long>();

        foreach (var item in page.Items)
        {
            if (item.Tipo == "receita")
            {
                receitasCents += item.AmountInCents;
                continue;
            }

            gastoCents += item.AmountInCents;
            gastoPorCategoria[item.CategoryId] =
                gastoPorCategoria.GetValueOrDefault(item.CategoryId) + item.AmountInCents;
        }

        // Só despesa entra em orçamento/"por categoria" (spec.md, decisão de escopo 3-4)
        // — orcamentoMensalCents de uma categoria de receita, se existir, é ignorado aqui.
        var budgetedCategories = (await _categoryRepository.ListAsync(query.AccountId, "despesa", cancellationToken))
            .Where(c => c.OrcamentoMensalCents is not null)
            .ToList();

        var orcamentoTotalCents = budgetedCategories.Sum(c => c.OrcamentoMensalCents!.Value);

        var porCategoria = budgetedCategories
            .Select(c => new CategorySummaryItem(
                c.Id, c.Nome, gastoPorCategoria.GetValueOrDefault(c.Id), c.OrcamentoMensalCents!.Value))
            .OrderByDescending(c => c.GastoCents)
            .ToList();

        // Mesmo cache-por-request de GetTransactionsQueryHandler — evita repetir
        // FindByAccountAndUserIdAsync pro mesmo autor entre os 5 últimos lançamentos.
        var labelCache = new Dictionary<string, string>();
        var ultimosLancamentos = new List<TransactionSummary>(RecentTransactionsCount);
        foreach (var item in page.Items.Take(RecentTransactionsCount))
        {
            if (!labelCache.TryGetValue(item.CreatedByUserId, out var label))
            {
                label = await CreatedByLabelResolver.ResolveAsync(
                    _membershipRepository, query.AccountId, item.CreatedByUserId, query.CallerUserId, cancellationToken);
                labelCache[item.CreatedByUserId] = label;
            }

            ultimosLancamentos.Add(TransactionSummary.FromQueryItem(item, label));
        }

        return Result.Success(new GetSummaryResult(
            query.Month,
            SaldoCents: receitasCents - gastoCents,
            ReceitasCents: receitasCents,
            GastoCents: gastoCents,
            OrcamentoTotalCents: orcamentoTotalCents,
            RestanteCents: orcamentoTotalCents - gastoCents,
            PorCategoria: porCategoria,
            UltimosLancamentos: ultimosLancamentos));
    }
}

public sealed record GetSummaryResult(
    string Month,
    long SaldoCents,
    long ReceitasCents,
    long GastoCents,
    long OrcamentoTotalCents,
    long RestanteCents,
    IReadOnlyList<CategorySummaryItem> PorCategoria,
    IReadOnlyList<TransactionSummary> UltimosLancamentos);

public sealed record CategorySummaryItem(
    string CategoryId,
    string Nome,
    long GastoCents,
    long OrcamentoMensalCents);
```

`page.Items` já vem ordenado mais recente primeiro
(`ScanIndexForward = false`, mesmo mecanismo de `GET /transactions`) —
`Take(5)` sobre a lista já ordenada é suficiente para
`ultimosLancamentos`, sem sort adicional.

`TransactionSummary`/`TransactionSummary.FromQueryItem` são
reaproveitados diretamente de
`GastosApp.Application.Transactions.Queries.GetTransactions` (mesmo
`record`, já público) — sem duplicar o shape de item de transação, e
garante por construção que `ultimosLancamentos` tem exatamente os
mesmos campos de `GET /transactions` (`spec.md`, decisão de escopo 5).
`CreatedByLabelResolver` (`internal static`, mesmo assembly
`GastosApp.Application`) também é reaproveitado sem mudança.

### `GetSummaryQueryValidator` (`Summary/Queries/GetSummary/GetSummaryQueryValidator.cs`)

```csharp
public sealed partial class GetSummaryQueryValidator : AbstractValidator<GetSummaryQuery>
{
    public GetSummaryQueryValidator()
    {
        RuleFor(q => q.Month)
            .NotEmpty().WithMessage("O parâmetro month é obrigatório.")
            .Matches(YearMonthRegex()).WithMessage("month deve estar no formato YYYY-MM.")
            .When(q => !string.IsNullOrEmpty(q.Month));
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonthRegex();
}
```
Mesmo regex já usado por `GetTransactionsQueryValidator.YearMonthRegex`
(rejeita `2026-13`, `2026/08`, etc.) — duplicado aqui em vez de
extraído para um helper compartilhado, seguindo o mesmo padrão já
aceito pelo projeto (`GetCategoriesQueryValidator` também duplica a
regra de `tipo` em vez de compartilhar com
`GetTransactionsQueryValidator`).

### `ApplicationServiceCollectionExtensions` — registro

```csharp
services.AddScoped<IValidator<GetSummaryQuery>, GetSummaryQueryValidator>(); // novo
```
(mantém os registros já existentes, incluindo os de `Transactions`/
`Categories`/`Members` — nada é removido).

## Infrastructure-layer

Nenhuma mudança. `DynamoDbTransactionRepository.QueryAsync` e
`DynamoDbCategoryRepository.ListAsync` já cobrem exatamente os dois
acessos que o Handler precisa — reaproveitados como estão.

## Api-layer

### `SummaryEndpoints` (`Endpoints/SummaryEndpoints.cs`, novo)

```csharp
public static class SummaryEndpoints
{
    public static IEndpointRouteBuilder MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/summary")
            .WithTags("Summary")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sem RoleEndpointFilters.Require — qualquer papel autenticado da conta
        // ativa pode consultar (spec.md, decisão de escopo 7 / US10).
        group.MapGet("/", GetSummary)
            .Produces<GetSummaryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSummary(
        [AsParameters] GetSummaryRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetSummaryQuery(currentAccount.AccountId!, currentAccount.UserId!, request.Month);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}

public record GetSummaryRequest(string Month = "");
```
Mesmo padrão de grupo já usado por `TransactionEndpoints`/
`CategoryEndpoints` (`RequireAuthorization()` +
`ResolveAccountEndpointFilter`, que já garante 401 sem token e resolve
`AccountId`/`UserId`/`Role` uma vez por request — nenhuma mudança
nesse filtro).

`Program.cs`: adicionar `app.MapSummaryEndpoints();` junto às demais
chamadas (`MapTransactionEndpoints()`, `MapCategoryEndpoints()`,
`MapMemberEndpoints()`).

### `AppJsonSerializerContext.cs` — novas entradas

```csharp
[JsonSerializable(typeof(GetSummaryResult))]
[JsonSerializable(typeof(CategorySummaryItem))]
[JsonSerializable(typeof(GetSummaryRequest))]
```
(`TransactionSummary`, reaproveitado em `UltimosLancamentos`, já está
registrado hoje.) `GetSummaryRequest` é bindado via `[AsParameters]`,
não passa pelo `JsonSerializerContext` de fato — mantido na lista só
por paralelismo com o padrão já adotado (`GetTransactionsRequest`/
`GetCategoriesRequest`).

## Mapeamento de erros

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `month` ausente, vazio ou fora do formato `YYYY-MM` (inclui mês inválido, ex.: `2026-13`) | `validation-error` | `Validation` | 400 |

Nenhum `Error` novo — reaproveita `Error.Validation` já existente.
`401` continua vindo de `ResolveAccountEndpointFilter` (sem token/conta
não resolvida), sem passar pelo `Handler`. Sem `403`/`404`/`422` — o
endpoint é liberado a qualquer papel e sempre retorna 200 (mesmo para
mês sem dados, `spec.md` decisão de escopo 6).

## Recursos AWS

Nenhum recurso novo. Reaproveita a tabela `GastosApp`, o índice base
(`PK`/`SK`) e o `GSI1`/`GSI2` já provisionados — a consulta de
transações do mês usa o mesmo access pattern AP1 já documentado em
`backend/docs/architecture.md` ("Transações de um mês"); a consulta de
categorias usa o mesmo `ListAsync` já usado por `GET /categories`. Sem
alteração em `backend/infra/terraform/`.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Application/GetSummaryQueryValidatorTests.cs`: `month` ausente/vazio
  → inválido; formatos inválidos (`2026-13`, `2026/08`, `agosto-2026`,
  `26-08`) → inválido; `2026-08` → válido
- `Application/GetSummaryQueryHandlerTests.cs` (mock
  `ITransactionRepository`/`ICategoryRepository`/`IMembershipRepository`):
  - `receitasCents`/`gastoCents`/`saldoCents` somados corretamente a
    partir de uma mistura de transações `despesa`/`receita` retornada
    pelo mock
  - `orcamentoTotalCents` soma só categorias `tipo="despesa"` com
    `orcamentoMensalCents` definido — categoria `despesa` sem
    orçamento e categoria `receita` (mesmo com orçamento) não entram
  - `porCategoria` inclui categoria com orçamento e gasto zero no mês
    (`gastoCents=0`, não omitida); ordenada por `gastoCents`
    decrescente
  - `restanteCents` negativo quando `gastoCents` > `orcamentoTotalCents`
    (sem exceção/erro)
  - mês sem nenhuma transação (`page.Items` vazio) → todos os totais
    zerados, `porCategoria` ainda reflete categorias com orçamento
    (gasto zero), `ultimosLancamentos` vazio
  - mais de 5 transações no mês → `ultimosLancamentos` traz só as 5
    primeiras de `page.Items` (mock retorna lista já ordenada,
    handler não reordena)
  - duas transações do mesmo `CreatedByUserId` entre as 5 últimas →
    só uma chamada a `FindByAccountAndUserIdAsync` (cache por request,
    mesmo teste já existente em
    `GetTransactionsQueryHandlerTests` mirrorado aqui)
  - `TransactionQueryFilter` passado ao mock tem `YearMonth` igual ao
    `Month` da query, `Tipo=null`, `CategoryId=null`,
    `Limit=int.MaxValue`

### Component tests (`backend/tests/GastosApp.ComponentTests/Summary/SummaryEndpointsTests.cs`, novo)

Cobre as 11 user stories do `spec.md` fim a fim (mock de
`ITransactionRepository`/`ICategoryRepository`/`IMembershipRepository`
via `WebApplicationFactory`, ver FEAT-03): resumo com dados (números
batendo com o cenário do mockup: saldo/receitas/gasto/orçamento
total/restante); `month` ausente → 400; `month` em formato inválido →
400; mês sem transações → 200 zerado; `porCategoria` só com despesa +
orçamento definido, ordenada por gasto; `ultimosLancamentos` limitado a
5 e ordenado; `restanteCents` negativo sem erro; isolamento entre
contas (dado de uma conta nunca aparece no resumo de outra); qualquer
papel (`Leitura`/`Lancar`/`Total`/`Titular`) recebe 200; 401 sem token.

## Critical Files

- `backend/src/GastosApp.Application/Summary/Queries/GetSummary/GetSummaryQuery.cs` (novo)
- `backend/src/GastosApp.Application/Summary/Queries/GetSummary/GetSummaryQueryValidator.cs` (novo)
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Api/Endpoints/SummaryEndpoints.cs` (novo)
- `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`
- `backend/src/GastosApp.Api/Program.cs` — `MapSummaryEndpoints()`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Transactions`/`Categories`/`Members`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar só a
  adição do novo `/summary` (`GET`, parâmetro `month`, schema de
  response, `400`/`401`), sem tocar `/transactions`, `/categories` ou
  `/members`
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/LocalStack): registrar transações de despesa e
  receita num mês, definir orçamento numa categoria de despesa,
  consultar `GET /summary?month=YYYY-MM` e conferir os números batendo
  manualmente com o que foi lançado; consultar um mês sem nenhum dado
  (200 zerado); tentar sem `month` (400)

## Decisões técnicas

1. **`Limit = int.MaxValue` no `TransactionQueryFilter`, sem cap de
   negócio.** Ver "Contexto técnico", decisão 1 — evita agregação
   silenciosamente incompleta; a salvaguarda de custo é o
   `MaxPaginationIterations` já existente no repositório.
2. **Reaproveitar `TransactionSummary`/`CreatedByLabelResolver` do
   módulo `Transactions` em vez de duplicar.** Os dois já são públicos/
   internal no mesmo assembly `GastosApp.Application` — mesma
   dependência same-layer já usada por outros Handlers do próprio
   módulo `Transactions` entre si; não introduz nenhuma dependência
   circular (`Summary` depende de `Transactions`, nunca o contrário).
3. **Nenhuma interface de repositório muda.** Tanto
   `ITransactionRepository.QueryAsync` quanto
   `ICategoryRepository.ListAsync` já expõem exatamente o que o
   Handler precisa — a agregação (soma, agrupamento por categoria,
   ordenação) é feita inteiramente em memória no Handler, como o
   roadmap já define para toda esta leva de features.
4. **Sem `RoleEndpointFilters.Require` no grupo `/summary`** — mesmo
   padrão de `GET /transactions`/`GET /categories` (sem filtro de
   papel = qualquer papel autenticado passa, já que
   `ResolveAccountEndpointFilter` roda antes e garante 401 sem
   token/conta).

## Decisões confirmadas com o usuário (revisão pós-plan)

1. **Decisão técnica 1 (`Limit = int.MaxValue`, sem cap de negócio,
   fail-loud acima de 25 páginas)** — confirmada com o usuário após
   revisão do trade-off: prioriza correção do dashboard financeiro
   sobre nunca falhar. Estimativa de volume pra estourar o cap
   existente (`MaxPaginationIterations = 25`): ~35-50 mil transações
   num único mês, várias ordens de grandeza acima do uso pessoal
   previsto pelo projeto — cenário hoje inexistente.
