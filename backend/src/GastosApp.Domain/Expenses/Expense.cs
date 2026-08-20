namespace GastosApp.Domain.Expenses;

public sealed class Expense
{
    public string Id { get; }
    public string UserId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public string CategoryId { get; }
    public DateOnly ExpenseDate { get; }
    public DateTimeOffset CreatedAt { get; }

    private Expense(
        string id,
        string userId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Description = description;
        AmountInCents = amountInCents;
        CategoryId = categoryId;
        ExpenseDate = expenseDate;
        CreatedAt = createdAt;
    }

    public static Expense Create(
        string userId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate)
    {
        return new Expense(
            Guid.NewGuid().ToString(),
            userId,
            description,
            amountInCents,
            categoryId,
            expenseDate,
            DateTimeOffset.UtcNow);
    }

    public static Expense Restore(
        string id,
        string userId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        DateTimeOffset createdAt)
    {
        return new Expense(id, userId, description, amountInCents, categoryId, expenseDate, createdAt);
    }
}
