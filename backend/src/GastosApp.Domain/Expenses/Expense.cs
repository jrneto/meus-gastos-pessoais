namespace GastosApp.Domain.Expenses;

public sealed class Expense
{
    public string Id { get; }
    public string AccountId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public string CategoryId { get; }
    public DateOnly ExpenseDate { get; }
    public DateTimeOffset CreatedAt { get; }

    private Expense(
        string id,
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        Description = description;
        AmountInCents = amountInCents;
        CategoryId = categoryId;
        ExpenseDate = expenseDate;
        CreatedAt = createdAt;
    }

    public static Expense Create(
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate)
    {
        return new Expense(
            Guid.NewGuid().ToString(),
            accountId,
            description,
            amountInCents,
            categoryId,
            expenseDate,
            DateTimeOffset.UtcNow);
    }

    public static Expense Restore(
        string id,
        string accountId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        DateTimeOffset createdAt)
    {
        return new Expense(id, accountId, description, amountInCents, categoryId, expenseDate, createdAt);
    }
}
