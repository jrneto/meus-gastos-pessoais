namespace GastosApp.Application.Common.Interfaces;

public sealed record ExpenseQueryPage(
    IReadOnlyList<ExpenseQueryItem> Items,
    string? NextCursor);
