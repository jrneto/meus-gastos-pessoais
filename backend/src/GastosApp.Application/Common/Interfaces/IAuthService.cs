namespace GastosApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public record RegisterResult(string UserId, string Email, string Name);
public record LoginResult(string AccessToken, int ExpiresIn, string UserId, string Name);
