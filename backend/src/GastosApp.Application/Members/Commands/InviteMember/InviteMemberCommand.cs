using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Members.Commands.InviteMember;

public sealed record InviteMemberCommand(string AccountId, string Email, string Role) : ICommand<Result<MemberResult>>;

public sealed class InviteMemberCommandHandler : ICommandHandler<InviteMemberCommand, Result<MemberResult>>
{
    private readonly IMembershipRepository _membershipRepository;

    public InviteMemberCommandHandler(IMembershipRepository membershipRepository)
    {
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<MemberResult>> Handle(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        var role = Enum.Parse<MembershipRole>(command.Role);
        var result = await _membershipRepository.CreateInviteAsync(command.AccountId, command.Email, role, cancellationToken);

        return result.Outcome switch
        {
            MembershipWriteOutcome.Success => Result.Success(MemberResult.FromEntity(result.Membership!)),
            _ => Result.Failure<MemberResult>(MembershipErrors.AlreadyExists)
        };
    }
}
