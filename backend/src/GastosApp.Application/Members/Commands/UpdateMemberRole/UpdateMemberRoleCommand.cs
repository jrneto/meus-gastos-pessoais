using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Members.Commands.UpdateMemberRole;

public sealed record UpdateMemberRoleCommand(string AccountId, string MembershipId, string Role) : ICommand<Result<MemberResult>>;

public sealed class UpdateMemberRoleCommandHandler : ICommandHandler<UpdateMemberRoleCommand, Result<MemberResult>>
{
    private readonly IMembershipRepository _membershipRepository;

    public UpdateMemberRoleCommandHandler(IMembershipRepository membershipRepository)
    {
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<MemberResult>> Handle(UpdateMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var membership = await _membershipRepository.GetByIdAsync(command.AccountId, command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.Failure<MemberResult>(MembershipErrors.NotFound);

        if (membership.Role == MembershipRole.Titular)
            return Result.Failure<MemberResult>(MembershipErrors.CannotModifyTitular);

        var role = Enum.Parse<MembershipRole>(command.Role);
        var result = await _membershipRepository.UpdateRoleAsync(command.AccountId, command.MembershipId, role, cancellationToken);

        return result.Outcome switch
        {
            MembershipWriteOutcome.Success => Result.Success(MemberResult.FromEntity(result.Membership!)),
            _ => Result.Failure<MemberResult>(MembershipErrors.NotFound)
        };
    }
}
