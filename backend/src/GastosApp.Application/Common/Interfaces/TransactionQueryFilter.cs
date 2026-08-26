namespace GastosApp.Application.Common.Interfaces;

public sealed record TransactionQueryFilter(
    string AccountId,
    string? Tipo,
    string? YearMonth,
    string? CategoryId,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int Limit);
