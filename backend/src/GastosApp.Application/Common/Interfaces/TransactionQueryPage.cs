namespace GastosApp.Application.Common.Interfaces;

public sealed record TransactionQueryPage(
    IReadOnlyList<TransactionQueryItem> Items,
    string? NextCursor);
