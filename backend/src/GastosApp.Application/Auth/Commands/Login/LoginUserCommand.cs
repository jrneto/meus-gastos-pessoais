using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Application.Auth.Commands.Login;

public record LoginUserCommand(string Email, string Password);

public class LoginUserCommandHandler
{
    private readonly IAuthService _authService;

    public LoginUserCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<LoginUserResult> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email é obrigatório.", nameof(command.Email));
        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Senha é obrigatória.", nameof(command.Password));

        var result = await _authService.LoginAsync(command.Email, command.Password, cancellationToken);
        return new LoginUserResult(result.accessToken, result.ExpiresIn, result.UserId);
    }
}

public record LoginUserResult(string AccessToken, int ExpiresIn, string UserId);
