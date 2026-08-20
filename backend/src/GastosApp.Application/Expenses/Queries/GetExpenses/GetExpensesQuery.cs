using System.Globalization;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Expenses.Queries.GetExpenses;

public sealed record GetExpensesQuery(
    string UserId,
    string? YearMonth,
    string? CategoryId,
    string? DateFrom,
    string? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int? Limit) : IQuery<Result<GetExpensesResult>>;

public sealed class GetExpensesQueryHandler : IQueryHandler<GetExpensesQuery, Result<GetExpensesResult>>
{
    private const int DefaultLimit = 20;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IExpenseRepository _expenseRepository;

    public GetExpensesQueryHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result<GetExpensesResult>> Handle(GetExpensesQuery query, CancellationToken cancellationToken)
    {
        var filter = new ExpenseQueryFilter(
            UserId: query.UserId,
            YearMonth: query.YearMonth,
            CategoryId: query.CategoryId,
            DateFrom: query.DateFrom is null ? null : DateOnly.ParseExact(query.DateFrom, DateFormat, CultureInfo.InvariantCulture),
            DateTo: query.DateTo is null ? null : DateOnly.ParseExact(query.DateTo, DateFormat, CultureInfo.InvariantCulture),
            MinAmountInCents: query.MinAmountInCents,
            MaxAmountInCents: query.MaxAmountInCents,
            Cursor: query.Cursor,
            Limit: query.Limit ?? DefaultLimit);

        var page = await _expenseRepository.QueryAsync(filter, cancellationToken);

        return Result.Success(GetExpensesResult.FromPage(page));
    }
}

public sealed record GetExpensesResult(
    IReadOnlyList<ExpenseSummary> Items,
    string? NextCursor)
{
    public static GetExpensesResult FromPage(ExpenseQueryPage page) => new(
        page.Items.Select(ExpenseSummary.FromQueryItem).ToList(),
        page.NextCursor);
}

public sealed record ExpenseSummary(
    string Id,
    string Description,
    long AmountInCents,
    string CategoryId,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt)
{
    public static ExpenseSummary FromQueryItem(ExpenseQueryItem item) => new(
        item.Id,
        item.Description,
        item.AmountInCents,
        item.CategoryId,
        item.ExpenseDate,
        item.CreatedAt);
}
