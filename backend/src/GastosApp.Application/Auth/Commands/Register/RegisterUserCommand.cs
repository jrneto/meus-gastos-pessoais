using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Application.Auth.Commands.Register;

public record RegisterUserCommand(string Email, string Password);

public class RegisterUserCommandHandler
{
    private readonly IAuthService _authService;

    public RegisterUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<RegisterUserResult> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email é obrigatório.", nameof(command.Email));
        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Senha é obrigatória.", nameof(command.Password));
        if (command.Password.Length < 8)
            throw new ArgumentException("Senha deve ter no mínimo 8 caracteres.", nameof(command.Password));

        var result = await _authService.RegisterAsync(command.Email, command.Password, cancellationToken);
        return new RegisterUserResult(result.UserId, result.Email);
    }
}

public record RegisterUserResult(string UserId, string Email);
