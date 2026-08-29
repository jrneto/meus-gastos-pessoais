using GastosApp.Domain.Users;

namespace GastosApp.Application.Common.Interfaces;

public sealed record CreateUserProfileResult(bool CpfAlreadyExists);

public interface IUserProfileRepository
{
    Task<CreateUserProfileResult> CreateAsync(UserProfile profile, CancellationToken cancellationToken = default);
    Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
