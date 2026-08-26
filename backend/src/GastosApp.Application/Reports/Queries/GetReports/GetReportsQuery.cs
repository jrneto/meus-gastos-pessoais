using System.Globalization;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Reports.Queries.GetReports;

public sealed record GetReportsQuery(
    string AccountId,
    string Period,
    string Date) : IQuery<Result<GetReportsResult>>;

public sealed class GetReportsQueryHandler : IQueryHandler<GetReportsQuery, Result<GetReportsResult>>
{
    // Sem cap de negócio — mesma decisão já tomada e confirmada com o usuário
    // na FEAT-23 (ver backend/specs/FEAT-23-resumo-mensal-dashboard/plan.md,
    // "Decisões confirmadas com o usuário"). A salvaguarda de custo continua
    // sendo o MaxPaginationIterations já existente no repositório.
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

        var (totalCents, gastoPorCategoria) = await SumDespesasAsync(query.AccountId, current, cancellationToken);

        var previousPage = await _transactionRepository.QueryAsync(
            BuildFilter(query.AccountId, previous), cancellationToken);
        var previousTotalCents = previousPage.Items.Sum(i => i.AmountInCents);

        // Só despesa entra aqui — este endpoint não cobre receitas (spec.md, decisão de escopo 4).
        var categories = await _categoryRepository.ListAsync(query.AccountId, "despesa", cancellationToken);
        var nomePorCategoria = categories.ToDictionary(c => c.Id, c => c.Nome);
        var orcamentoPorCategoria = categories
            .Where(c => c.OrcamentoMensalCents is not null)
            .ToDictionary(c => c.Id, c => c.OrcamentoMensalCents!.Value);

        var porCategoria = gastoPorCategoria
            .Where(kv => kv.Value > 0)
            .Select(kv => new ReportCategoryItem(
                kv.Key,
                // Defesa contra categoria excluída depois de já ter transações lançadas
                // (hoje bloqueado por ExistsByCategoryAsync na exclusão — ver DynamoDbCategoryRepository
                // — mas o Handler não deve quebrar se esse invariante já tiver sido violado por dado legado).
                nomePorCategoria.GetValueOrDefault(kv.Key, kv.Key),
                kv.Value))
            .OrderByDescending(c => c.GastoCents)
            .ToList();

        var maiorGasto = porCategoria.Count == 0
            ? null
            : new ReportTopCategory(
                porCategoria[0].CategoryId,
                porCategoria[0].Nome,
                porCategoria[0].GastoCents,
                orcamentoPorCategoria.TryGetValue(porCategoria[0].CategoryId, out var orcamento)
                    ? Math.Round((decimal)porCategoria[0].GastoCents / orcamento * 100, 1)
                    : null);

        // Regra pra divisão por zero (spec.md, decisão de escopo 6): período anterior zerado e
        // atual também zerado -> 0; período anterior zerado e atual com gasto -> não computável (null).
        decimal? variacaoPercentual = previousTotalCents == 0
            ? (totalCents == 0 ? 0m : null)
            : Math.Round((decimal)(totalCents - previousTotalCents) / previousTotalCents * 100, 1);

        return Result.Success(new GetReportsResult(
            query.Period,
            current.Start,
            current.End,
            totalCents,
            variacaoPercentual,
            porCategoria,
            maiorGasto));
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

public sealed record ReportCategoryItem(
    string CategoryId,
    string Nome,
    long GastoCents);

public sealed record ReportTopCategory(
    string CategoryId,
    string Nome,
    long GastoCents,
    decimal? PercentualOrcamento);
