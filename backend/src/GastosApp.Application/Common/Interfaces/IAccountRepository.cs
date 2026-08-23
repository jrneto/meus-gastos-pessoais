namespace GastosApp.Application.Common.Interfaces;

public sealed record CreateAccountResult(string AccountId, bool AlreadyExisted);

public interface IAccountRepository
{
    Task<string?> FindAccountIdByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<CreateAccountResult> CreateAsync(string userId, CancellationToken cancellationToken = default);
}
