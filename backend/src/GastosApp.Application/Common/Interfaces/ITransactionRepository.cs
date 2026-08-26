using GastosApp.Domain.Transactions;

namespace GastosApp.Application.Common.Interfaces;

public interface ITransactionRepository
{
    Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<TransactionQueryPage> QueryAsync(TransactionQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(string accountId, string transactionId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
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
