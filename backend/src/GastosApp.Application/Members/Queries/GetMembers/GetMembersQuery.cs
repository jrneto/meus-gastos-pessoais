using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Members.Queries.GetMembers;

public sealed record GetMembersQuery(string AccountId) : IQuery<Result<GetMembersResult>>;

public sealed class GetMembersQueryHandler : IQueryHandler<GetMembersQuery, Result<GetMembersResult>>
{
    private readonly IMembershipRepository _membershipRepository;

    public GetMembersQueryHandler(IMembershipRepository membershipRepository)
    {
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<GetMembersResult>> Handle(GetMembersQuery query, CancellationToken cancellationToken)
    {
        var memberships = await _membershipRepository.ListAsync(query.AccountId, cancellationToken);
        return Result.Success(GetMembersResult.FromEntities(memberships));
    }
}
