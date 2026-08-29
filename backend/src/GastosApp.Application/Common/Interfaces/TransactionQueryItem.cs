namespace GastosApp.Application.Common.Interfaces;

public sealed record TransactionQueryItem(
    string Id,
    string Description,
    long AmountInCents,
    string CategoryId,
    string Tipo,
    DateOnly Date,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);
