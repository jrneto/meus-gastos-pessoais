namespace GastosApp.Domain.Transactions;

public sealed class Transaction
{
    public string Id { get; }
    public string AccountId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public string CategoryId { get; }
    public string Tipo { get; }
    public DateOnly Date { get; }
    public string CreatedByUserId { get; }
    public DateTimeOffset CreatedAt { get; }

    private Transaction(
        string id,
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        string tipo,
        DateOnly date,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        Description = description;
        AmountInCents = amountInCents;
        CategoryId = categoryId;
        Tipo = tipo;
        Date = date;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public static Transaction Create(
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        string tipo,
        DateOnly date,
        string createdByUserId)
    {
        return new Transaction(
            Guid.NewGuid().ToString(),
            accountId,
            description,
            amountInCents,
            categoryId,
            tipo,
            date,
            createdByUserId,
            DateTimeOffset.UtcNow);
    }

    public static Transaction Restore(
        string id,
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        string tipo,
        DateOnly date,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        return new Transaction(id, accountId, description, amountInCents, categoryId, tipo, date, createdByUserId, createdAt);
    }
}
