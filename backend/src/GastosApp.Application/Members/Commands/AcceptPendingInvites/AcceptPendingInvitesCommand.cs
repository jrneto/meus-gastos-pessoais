using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Members.Commands.AcceptPendingInvites;

// Aceita todo convite pendente pro e-mail do usuário que acabou de logar
// (Status=ConvitePendente -> Ativo, em todas as contas em que houver) e, se
// houver ao menos um, troca a conta ativa (AccountPointer) pra a do convite
// mais recente. Nunca falha por regra de negócio — mesmo espírito de
// EnsureAccountCommand (só propaga exceção de infraestrutura genuína,
// capturada por quem despacha, ver LoginUserCommandHandler).
public sealed record AcceptPendingInvitesCommand(string UserId, string Email) : ICommand<Result<AcceptPendingInvitesResult>>;

public sealed record AcceptPendingInvitesResult(string? SwitchedToAccountId);

public sealed class AcceptPendingInvitesCommandHandler : ICommandHandler<AcceptPendingInvitesCommand, Result<AcceptPendingInvitesResult>>
{
    private readonly IMembershipRepository _membershipRepository;
    private readonly IAccountRepository _accountRepository;

    public AcceptPendingInvitesCommandHandler(IMembershipRepository membershipRepository, IAccountRepository accountRepository)
    {
        _membershipRepository = membershipRepository;
        _accountRepository = accountRepository;
    }

    public async ValueTask<Result<AcceptPendingInvitesResult>> Handle(AcceptPendingInvitesCommand command, CancellationToken cancellationToken)
    {
        var accepted = await _membershipRepository.AcceptPendingInvitesByEmailAsync(command.Email, command.UserId, cancellationToken);
        if (accepted.Count == 0)
            return Result.Success(new AcceptPendingInvitesResult(null));

        var mostRecent = accepted.OrderByDescending(a => a.CreatedAt).First();
        await _accountRepository.SetActiveAccountAsync(command.UserId, mostRecent.AccountId, cancellationToken);

        return Result.Success(new AcceptPendingInvitesResult(mostRecent.AccountId));
    }
}
