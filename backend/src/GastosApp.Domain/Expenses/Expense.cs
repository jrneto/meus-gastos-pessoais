namespace GastosApp.Domain.Expenses;

public sealed class Expense
{
    public string Id { get; }
    public string UserId { get; }
    public string Description { get; }
    public long AmountInCents { get; }
    public ExpenseCategory Category { get; }
    public DateOnly ExpenseDate { get; }
    public DateTimeOffset CreatedAt { get; }

    private Expense(
        string id,
        string userId,
        string description,
        long amountInCents,
        ExpenseCategory category,
        DateOnly expenseDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Description = description;
        AmountInCents = amountInCents;
        Category = category;
        ExpenseDate = expenseDate;
        CreatedAt = createdAt;
    }

    public static Expense Create(
        string userId,
        string description,
        long amountInCents,
        ExpenseCategory category,
        DateOnly expenseDate)
    {
        return new Expense(
            Guid.NewGuid().ToString(),
            userId,
            description,
            amountInCents,
            category,
            expenseDate,
            DateTimeOffset.UtcNow);
    }
}
