using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<Result<RefreshTokenResult>>;

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
{
    private readonly IAuthService _authService;

    public RefreshTokenCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<Result<RefreshTokenResult>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
            return Result.Failure<RefreshTokenResult>(AuthErrors.RefreshTokenMissing);

        var result = await _authService.RefreshAsync(command.RefreshToken, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<RefreshTokenResult>(result.Error!);

        return Result.Success(RefreshTokenResult.FromRefreshResult(result.Value));
    }
}

public record RefreshTokenResult(string AccessToken, int ExpiresIn, string UserId)
{
    public static RefreshTokenResult FromRefreshResult(RefreshResult result) =>
        new(result.AccessToken, result.ExpiresIn, result.UserId);
}
