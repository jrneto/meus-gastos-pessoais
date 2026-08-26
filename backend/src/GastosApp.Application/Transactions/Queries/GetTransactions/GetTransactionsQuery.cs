using System.Globalization;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Common;
using Mediator;

namespace GastosApp.Application.Transactions.Queries.GetTransactions;

public sealed record GetTransactionsQuery(
    string AccountId,
    string CallerUserId,
    string? Tipo,
    string? YearMonth,
    string? CategoryId,
    string? DateFrom,
    string? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int? Limit) : IQuery<Result<GetTransactionsResult>>;

public sealed class GetTransactionsQueryHandler : IQueryHandler<GetTransactionsQuery, Result<GetTransactionsResult>>
{
    private const int DefaultLimit = 20;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ITransactionRepository _transactionRepository;
    private readonly IMembershipRepository _membershipRepository;

    public GetTransactionsQueryHandler(ITransactionRepository transactionRepository, IMembershipRepository membershipRepository)
    {
        _transactionRepository = transactionRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<GetTransactionsResult>> Handle(GetTransactionsQuery query, CancellationToken cancellationToken)
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
            Cursor: query.Cursor,
            Limit: query.Limit ?? DefaultLimit);

        var page = await _transactionRepository.QueryAsync(filter, cancellationToken);

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
                    _membershipRepository, query.AccountId, item.CreatedByUserId, query.CallerUserId, cancellationToken);
                labelCache[item.CreatedByUserId] = label;
            }

            items.Add(TransactionSummary.FromQueryItem(item, label));
        }

        return Result.Success(new GetTransactionsResult(items, page.NextCursor));
    }
}

public sealed record GetTransactionsResult(
    IReadOnlyList<TransactionSummary> Items,
    string? NextCursor);

public sealed record TransactionSummary(
    string Id,
    string Description,
    long AmountInCents,
    string CategoryId,
    string Tipo,
    DateOnly Date,
    string CreatedByUserId,
    string CreatedByLabel,
    DateTimeOffset CreatedAt)
{
    public static TransactionSummary FromQueryItem(TransactionQueryItem item, string createdByLabel) => new(
        item.Id,
        item.Description,
        item.AmountInCents,
        item.CategoryId,
        item.Tipo,
        item.Date,
        item.CreatedByUserId,
        createdByLabel,
        item.CreatedAt);
}
