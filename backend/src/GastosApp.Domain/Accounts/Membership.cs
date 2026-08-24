namespace GastosApp.Domain.Accounts;

// Só "Titular" por enquanto (papel do usuário que criou a conta
// automaticamente). Níveis de acesso pra membros convidados
// (Leitura/Lancar/Total) entram na FEAT-20.
public enum MembershipRole
{
    Titular
}

public sealed class Membership
{
    public string AccountId { get; }
    public string UserId { get; }
    public MembershipRole Role { get; }
    public DateTimeOffset CreatedAt { get; }

    private Membership(string accountId, string userId, MembershipRole role, DateTimeOffset createdAt)
    {
        AccountId = accountId;
        UserId = userId;
        Role = role;
        CreatedAt = createdAt;
    }

    public static Membership CreateTitular(string accountId, string userId)
    {
        return new Membership(accountId, userId, MembershipRole.Titular, DateTimeOffset.UtcNow);
    }

    public static Membership Restore(string accountId, string userId, MembershipRole role, DateTimeOffset createdAt)
    {
        return new Membership(accountId, userId, role, createdAt);
    }
}
