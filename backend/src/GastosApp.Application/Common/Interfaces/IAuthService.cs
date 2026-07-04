namespace GastosApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public record RegisterResult(string UserId, string Email);
public record LoginResult(string accessToken, int ExpiresIn, string UserId);
