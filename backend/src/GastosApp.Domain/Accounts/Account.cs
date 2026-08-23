namespace GastosApp.Domain.Accounts;

public sealed class Account
{
    public string Id { get; }
    public DateTimeOffset CreatedAt { get; }

    private Account(string id, DateTimeOffset createdAt)
    {
        Id = id;
        CreatedAt = createdAt;
    }

    public static Account Create()
    {
        return new Account(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
    }

    public static Account Restore(string id, DateTimeOffset createdAt)
    {
        return new Account(id, createdAt);
    }
}
