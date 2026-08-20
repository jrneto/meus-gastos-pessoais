namespace GastosApp.Application.Common.Interfaces;

public sealed record ExpenseQueryFilter(
    string UserId,
    string? YearMonth,
    string? CategoryId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int Limit);
