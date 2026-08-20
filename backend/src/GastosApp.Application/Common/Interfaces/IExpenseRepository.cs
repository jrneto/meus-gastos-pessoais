using GastosApp.Domain.Expenses;

namespace GastosApp.Application.Common.Interfaces;

public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
    Task<Expense?> GetByIdAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryAsync(string userId, string categoryId, CancellationToken cancellationToken = default);
    Task<Expense?> UpdateAsync(
        string userId,
        string expenseId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        CancellationToken cancellationToken = default);
}
