using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Expenses;
using Mediator;

namespace GastosApp.Application.Expenses.Commands.RegisterExpense;

public sealed record RegisterExpenseCommand(
    string UserId,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate) : ICommand<Result<RegisterExpenseResult>>;

public sealed class RegisterExpenseCommandHandler : ICommandHandler<RegisterExpenseCommand, Result<RegisterExpenseResult>>
{
    private readonly IExpenseRepository _expenseRepository;

    public RegisterExpenseCommandHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result<RegisterExpenseResult>> Handle(RegisterExpenseCommand command, CancellationToken cancellationToken)
    {
        var category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);

        var expense = Expense.Create(
            command.UserId,
            command.Description,
            command.AmountInCents,
            category,
            command.ExpenseDate);

        await _expenseRepository.SaveAsync(expense, cancellationToken);

        return Result.Success(RegisterExpenseResult.FromExpense(expense));
    }
}

public record RegisterExpenseResult(
    string Id,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt)
{
    public static RegisterExpenseResult FromExpense(Expense expense) => new(
        expense.Id,
        expense.Description,
        expense.AmountInCents,
        expense.Category.ToString(),
        expense.ExpenseDate,
        expense.CreatedAt);
}
