using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Members.Commands.RemoveMember;

public sealed record RemoveMemberCommand(string AccountId, string MembershipId) : ICommand<Result>;

public sealed class RemoveMemberCommandHandler : ICommandHandler<RemoveMemberCommand, Result>
{
    private readonly IMembershipRepository _membershipRepository;

    public RemoveMemberCommandHandler(IMembershipRepository membershipRepository)
    {
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await _membershipRepository.GetByIdAsync(command.AccountId, command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.Failure(MembershipErrors.NotFound);

        if (membership.Role == MembershipRole.Titular)
            return Result.Failure(MembershipErrors.CannotRemoveTitular);

        var deleted = await _membershipRepository.DeleteAsync(command.AccountId, command.MembershipId, cancellationToken);
        return deleted ? Result.Success() : Result.Failure(MembershipErrors.NotFound);
    }
}
