namespace GastosApp.Domain.Users;

public sealed class UserProfile
{
    public string UserId { get; }
    public string Name { get; }
    public string PhoneNumber { get; }
    public string Cpf { get; }
    public DateTimeOffset CreatedAt { get; }

    private UserProfile(string userId, string name, string phoneNumber, string cpf, DateTimeOffset createdAt)
    {
        UserId = userId;
        Name = name;
        PhoneNumber = phoneNumber;
        Cpf = cpf;
        CreatedAt = createdAt;
    }

    public static UserProfile Create(string userId, string name, string phoneNumber, string cpf) =>
        new(userId, name, phoneNumber, cpf, DateTimeOffset.UtcNow);

    public static UserProfile Restore(string userId, string name, string phoneNumber, string cpf, DateTimeOffset createdAt) =>
        new(userId, name, phoneNumber, cpf, createdAt);
}
