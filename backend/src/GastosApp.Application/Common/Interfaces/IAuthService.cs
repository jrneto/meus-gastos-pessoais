using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<RegisterResult>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public record RegisterResult(string UserId, string Email);
public record LoginResult(string AccessToken, int ExpiresIn, string UserId);
