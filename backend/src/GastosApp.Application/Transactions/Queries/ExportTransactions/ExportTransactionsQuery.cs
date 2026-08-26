using System.Globalization;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Common;
using Mediator;

namespace GastosApp.Application.Transactions.Queries.ExportTransactions;

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
