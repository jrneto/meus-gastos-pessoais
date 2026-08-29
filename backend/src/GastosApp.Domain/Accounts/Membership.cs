namespace GastosApp.Domain.Accounts;

// Titular: papel fixo de quem criou a conta (FEAT-19). Leitura/Lancar/Total:
// níveis de acesso atribuíveis por convite (FEAT-20) — nunca "Titular".
public enum MembershipRole
{
    Titular,
    Leitura,
    Lancar,
    Total
}

// Ativo: membro resolvido (Titular sempre nasce assim; convidado, a partir
// do primeiro login cujo e-mail bate com o convite). ConvitePendente: convite
// criado mas ainda não aceito (UserId ainda não resolvido).
public enum MembershipStatus
{
    Ativo,
    ConvitePendente
}

public sealed class Membership
{
    public string Id { get; }
    public string AccountId { get; }
    public string? UserId { get; }
    public string Email { get; }
    public MembershipRole Role { get; }
    public MembershipStatus Status { get; }
    public DateTimeOffset CreatedAt { get; }

    private Membership(
        string id,
        string accountId,
        string? userId,
        string email,
        MembershipRole role,
        MembershipStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        UserId = userId;
        Email = email;
        Role = role;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Membership CreateTitular(string accountId, string userId, string email)
    {
        return new Membership(
            Guid.NewGuid().ToString(), accountId, userId, email, MembershipRole.Titular, MembershipStatus.Ativo, DateTimeOffset.UtcNow);
    }

    public static Membership CreateInvite(string accountId, string email, MembershipRole role)
    {
        return new Membership(
            Guid.NewGuid().ToString(), accountId, null, email, role, MembershipStatus.ConvitePendente, DateTimeOffset.UtcNow);
    }

    public static Membership Restore(
        string id,
        string accountId,
        string? userId,
        string email,
        MembershipRole role,
        MembershipStatus status,
        DateTimeOffset createdAt)
    {
        return new Membership(id, accountId, userId, email, role, status, createdAt);
    }
}
