using GastosApp.Application.Accounts;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Members.Queries.ResolveMembership;

// Substitui a antiga ResolveAccountIdQuery (FEAT-19): além do accountId,
// resolve o Membership do próprio chamador na conta ativa — usado por
// ResolveAccountEndpointFilter pra popular CurrentAccountContext em toda
// rota autenticada de Category/Expense/Members. Só lê — nunca cria (ao
// contrário de EnsureAccountCommand). Ausência de conta ou de Membership
// aqui é sempre 401 (account-not-found), nunca auto-cura — situação que só
// ocorreria por dado corrompido/manual.
public sealed record ResolveMembershipQuery(string UserId) : IQuery<Result<ResolveMembershipResult>>;

public sealed record ResolveMembershipResult(string AccountId, string MembershipId, MembershipRole Role);

public sealed class ResolveMembershipQueryHandler : IQueryHandler<ResolveMembershipQuery, Result<ResolveMembershipResult>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IMembershipRepository _membershipRepository;

    public ResolveMembershipQueryHandler(IAccountRepository accountRepository, IMembershipRepository membershipRepository)
    {
        _accountRepository = accountRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<ResolveMembershipResult>> Handle(ResolveMembershipQuery query, CancellationToken cancellationToken)
    {
        var accountId = await _accountRepository.FindAccountIdByUserIdAsync(query.UserId, cancellationToken);
        if (accountId is null)
            return Result.Failure<ResolveMembershipResult>(AccountErrors.NotResolved);

        var membership = await _membershipRepository.FindByAccountAndUserIdAsync(accountId, query.UserId, cancellationToken);
        // FEAT-41: um membro Inativo continua achável por FindByAccountAndUserIdAsync
        // (GSI1PK permanece USER#<userId> mesmo depois da inativação, ver
        // CreatedByLabelResolver), mas nunca resolve como membro utilizável —
        // é aqui que ele perde acesso à conta, mesmo erro de "conta não
        // resolvida" que já existe hoje pra um Membership ausente.
        if (membership is null || membership.Status != MembershipStatus.Ativo)
            return Result.Failure<ResolveMembershipResult>(AccountErrors.NotResolved);

        return Result.Success(new ResolveMembershipResult(accountId, membership.Id, membership.Role));
    }
}
