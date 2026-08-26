using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Common;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using Mediator;

namespace GastosApp.Application.Summary.Queries.GetSummary;

public sealed record GetSummaryQuery(
    string AccountId,
    string CallerUserId,
    string Month) : IQuery<Result<GetSummaryResult>>;

public sealed class GetSummaryQueryHandler : IQueryHandler<GetSummaryQuery, Result<GetSummaryResult>>
{
    // Sem cap de negócio — buscar uma página parcial e somar como se fosse o
    // total do mês produziria um resumo silenciosamente incorreto (ver
    // plan.md, "Contexto técnico", decisão 1). A única salvaguarda é o
    // MaxPaginationIterations já existente dentro de
    // ITransactionRepository.QueryAsync (DynamoDbTransactionRepository).
    private const int NoTruncationLimit = int.MaxValue;
    private const int RecentTransactionsCount = 5;
    private const string TipoDespesa = "despesa";
    private const string TipoReceita = "receita";

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
        // access pattern AP1 de backend/docs/architecture.md; sem CategoryId no
        // filtro (índice base, não GSI1) e sem Tipo (o resumo precisa dos dois).
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
            if (item.Tipo == TipoReceita)
            {
                receitasCents += item.AmountInCents;
                continue;
            }

            gastoCents += item.AmountInCents;
            gastoPorCategoria[item.CategoryId] =
                gastoPorCategoria.GetValueOrDefault(item.CategoryId) + item.AmountInCents;
        }

        // Só despesa entra em orçamento/"por categoria" (spec.md, decisão de
        // escopo 3-4) — orcamentoMensalCents de uma categoria de receita, se
        // existir, é ignorado aqui. Independe do mês consultado: orçamento é um
        // valor recorrente por categoria (FEAT-21), não um dado histórico.
        var budgetedCategories = (await _categoryRepository.ListAsync(query.AccountId, TipoDespesa, cancellationToken))
            .Where(c => c.OrcamentoMensalCents is not null)
            .ToList();

        var orcamentoTotalCents = budgetedCategories.Sum(c => c.OrcamentoMensalCents!.Value);

        var porCategoria = budgetedCategories
            .Select(c => new CategorySummaryItem(
                c.Id, c.Nome, gastoPorCategoria.GetValueOrDefault(c.Id), c.OrcamentoMensalCents!.Value))
            .OrderByDescending(c => c.GastoCents)
            .ToList();

        // page.Items já vem mais recente primeiro (ScanIndexForward=false, mesmo
        // mecanismo de GET /transactions) — Take(5) direto, sem sort adicional.
        // Mesmo cache-por-request de GetTransactionsQueryHandler: evita repetir
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
