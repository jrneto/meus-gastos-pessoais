using GastosApp.Domain.Expenses;

namespace GastosApp.Application.Common.Interfaces;

public sealed record ExpenseQueryItem(
    string Id,
    string Description,
    long AmountInCents,
    ExpenseCategory Category,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt);
