using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Register;

public sealed record RegisterUserCommand(string Email, string Password) : ICommand<Result<RegisterUserResult>>;

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    private readonly IAuthService _authService;

    public RegisterUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async ValueTask<Result<RegisterUserResult>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Failure<RegisterUserResult>(AuthErrors.Validation("Email é obrigatório."));
        if (string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<RegisterUserResult>(AuthErrors.Validation("Senha é obrigatória."));
        if (command.Password.Length < 8)
            return Result.Failure<RegisterUserResult>(AuthErrors.Validation("Senha deve ter no mínimo 8 caracteres."));

        var result = await _authService.RegisterAsync(command.Email, command.Password, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<RegisterUserResult>(result.Error!);

        return Result.Success(new RegisterUserResult(result.Value.UserId, result.Value.Email));
    }
}

public record RegisterUserResult(string UserId, string Email);