namespace GastosApp.Application.Common.Interfaces;

public sealed record CreateAccountResult(string AccountId, bool AlreadyExisted);

public interface IAccountRepository
{
    Task<string?> FindAccountIdByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<CreateAccountResult> CreateAsync(string userId, string email, CancellationToken cancellationToken = default);

    // FEAT-20: sobrescreve o AccountPointer (troca deliberada de conta ativa,
    // efeito colateral de aceitar um convite no login — ver plan.md).
    Task SetActiveAccountAsync(string userId, string accountId, CancellationToken cancellationToken = default);
}
