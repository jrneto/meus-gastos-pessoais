namespace GastosApp.Application.Common.Interfaces;

public sealed record ExpenseQueryItem(
    string Id,
    string Description,
    long AmountInCents,
    string CategoryId,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt);
