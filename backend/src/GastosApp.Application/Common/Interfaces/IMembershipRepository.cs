using GastosApp.Domain.Accounts;

namespace GastosApp.Application.Common.Interfaces;

public enum MembershipWriteOutcome
{
    Success,
    EmailConflict,
    NotFound
}

public sealed record MembershipWriteResult(MembershipWriteOutcome Outcome, Membership? Membership)
{
    public static MembershipWriteResult Success(Membership membership) => new(MembershipWriteOutcome.Success, membership);
    public static MembershipWriteResult EmailConflict() => new(MembershipWriteOutcome.EmailConflict, null);
    public static MembershipWriteResult NotFound() => new(MembershipWriteOutcome.NotFound, null);
}

public sealed record AcceptedInvite(string AccountId, DateTimeOffset CreatedAt);

public interface IMembershipRepository
{
    Task<IReadOnlyList<Membership>> ListAsync(string accountId, CancellationToken cancellationToken = default);
    Task<Membership?> GetByIdAsync(string accountId, string membershipId, CancellationToken cancellationToken = default);

    // Resolve o Membership do próprio chamador na conta ativa (via GSI1,
    // GSI1PK=USER#<userId> AND GSI1SK=ACCOUNT#<accountId>) — usado por
    // ResolveMembershipQuery pra popular CurrentAccountContext.Role.
    Task<Membership?> FindByAccountAndUserIdAsync(string accountId, string userId, CancellationToken cancellationToken = default);

    Task<MembershipWriteResult> CreateInviteAsync(string accountId, string email, MembershipRole role, CancellationToken cancellationToken = default);
    Task<MembershipWriteResult> UpdateRoleAsync(string accountId, string membershipId, MembershipRole role, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string membershipId, CancellationToken cancellationToken = default);

    // Aceita (Status=Ativo) todo convite pendente pro e-mail informado, em
    // qualquer conta — chamado no login (AcceptPendingInvitesCommand).
    Task<IReadOnlyList<AcceptedInvite>> AcceptPendingInvitesByEmailAsync(string email, string userId, CancellationToken cancellationToken = default);
}
