using GastosApp.Domain.Expenses;

namespace GastosApp.Application.Common.Interfaces;

public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string expenseId, CancellationToken cancellationToken = default);
    Task<Expense?> GetByIdAsync(string accountId, string expenseId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
    Task<Expense?> UpdateAsync(
        string accountId,
        string expenseId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        CancellationToken cancellationToken = default);
}
