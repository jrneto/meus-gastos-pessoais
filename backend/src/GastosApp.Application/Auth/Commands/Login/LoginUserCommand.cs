using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<Result<LoginUserResult>>;

public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<LoginUserResult>>
{
    private readonly IAuthService _authService;

    public LoginUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<Result<LoginUserResult>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Failure<LoginUserResult>(AuthErrors.Validation("Email é obrigatório."));
        if (string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<LoginUserResult>(AuthErrors.Validation("Senha é obrigatória."));

        var result = await _authService.LoginAsync(command.Email, command.Password, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<LoginUserResult>(result.Error!);

        return Result.Success(new LoginUserResult(result.Value.AccessToken, result.Value.ExpiresIn, result.Value.UserId));
    }
}

public record LoginUserResult(string AccessToken, int ExpiresIn, string UserId);
