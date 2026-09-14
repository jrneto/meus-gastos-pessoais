using GastosApp.Domain.Transactions;

namespace GastosApp.Application.Common.Interfaces;

public interface ITransactionRepository
{
    Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<TransactionQueryPage> QueryAsync(TransactionQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);

    // Existência de qualquer transação lançada por esse userId nesta conta
    // (FEAT-41) — usado só por RemoveMemberCommandHandler pra decidir inativar
    // o Membership em vez de removê-lo de fato. Sem GSI dedicado (ver
    // plan.md, "Recursos AWS"/decisão técnica 2): Query por PK, filtrando
    // CreatedByUserId.
    Task<bool> ExistsByCreatedByUserIdAsync(string accountId, string userId, CancellationToken cancellationToken = default);
    Task<Transaction?> UpdateAsync(
        string accountId,
        string transactionId,
        string description,
        long amountInCents,
        string categoryId,
        string tipo,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
