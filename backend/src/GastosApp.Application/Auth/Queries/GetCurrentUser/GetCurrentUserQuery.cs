using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Users;
using Mediator;

namespace GastosApp.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(string UserId, string? Email) : IQuery<Result<UserInfoResult>>;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Result<UserInfoResult>>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetCurrentUserQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async ValueTask<Result<UserInfoResult>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        // Sem migração de dados (backlog.md): usuário cadastrado antes desta feature
        // não tem UserProfile — campos voltam null, sem erro (spec.md não define esse
        // caso como falha).
        var profile = await _userProfileRepository.FindByUserIdAsync(query.UserId, cancellationToken);
        return Result.Success(UserInfoResult.FromEntity(query.UserId, query.Email, profile));
    }
}

public sealed record UserInfoResult(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf)
{
    public static UserInfoResult FromEntity(string userId, string? email, UserProfile? profile) =>
        new(userId, email, profile?.Name, profile?.PhoneNumber, profile?.Cpf);
}
