using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Members.Commands.RemoveMember;

public sealed record RemoveMemberCommand(string AccountId, string MembershipId) : ICommand<Result>;

public sealed class RemoveMemberCommandHandler : ICommandHandler<RemoveMemberCommand, Result>
{
    private readonly IMembershipRepository _membershipRepository;
    private readonly ITransactionRepository _transactionRepository;

    public RemoveMemberCommandHandler(IMembershipRepository membershipRepository, ITransactionRepository transactionRepository)
    {
        _membershipRepository = membershipRepository;
        _transactionRepository = transactionRepository;
    }

    public async ValueTask<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await _membershipRepository.GetByIdAsync(command.AccountId, command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.Failure(MembershipErrors.NotFound);

        if (membership.Role == MembershipRole.Titular)
            return Result.Failure(MembershipErrors.CannotRemoveTitular);

        if (membership.Status == MembershipStatus.Inativo)
            return Result.Failure(MembershipErrors.MemberAlreadyInactive);

        // FEAT-41: um membro Ativo que já lançou transação é inativado em vez
        // de removido de fato, pra createdByLabel continuar mostrando o e-mail
        // dele. ConvitePendente nunca tem transação possível (UserId é null),
        // segue removido de fato como sempre.
        if (membership.Status == MembershipStatus.Ativo)
        {
            var hasTransactions = await _transactionRepository.ExistsByCreatedByUserIdAsync(
                command.AccountId, membership.UserId!, cancellationToken);

            if (hasTransactions)
            {
                var inactivated = await _membershipRepository.InactivateAsync(
                    command.AccountId, command.MembershipId, cancellationToken);
                return inactivated ? Result.Success() : Result.Failure(MembershipErrors.NotFound);
            }
        }

        var deleted = await _membershipRepository.DeleteAsync(command.AccountId, command.MembershipId, cancellationToken);
        return deleted ? Result.Success() : Result.Failure(MembershipErrors.NotFound);
    }
}
