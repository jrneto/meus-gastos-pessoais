using GastosApp.Domain.Expenses;

namespace GastosApp.Application.Common.Interfaces;

public interface IExpenseRepository
{
    Task SaveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default);
}
