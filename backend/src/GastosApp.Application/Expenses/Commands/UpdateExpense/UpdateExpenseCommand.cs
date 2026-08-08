using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Expenses;
using Mediator;

namespace GastosApp.Application.Expenses.Commands.UpdateExpense;

public sealed record UpdateExpenseCommand(
    string UserId,
    string ExpenseId,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate) : ICommand<Result<UpdateExpenseResult>>;

public sealed class UpdateExpenseCommandHandler : ICommandHandler<UpdateExpenseCommand, Result<UpdateExpenseResult>>
{
    private readonly IExpenseRepository _expenseRepository;

    public UpdateExpenseCommandHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result<UpdateExpenseResult>> Handle(UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        var category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);

        var updated = await _expenseRepository.UpdateAsync(
            command.UserId,
            command.ExpenseId,
            command.Description,
            command.AmountInCents,
            category,
            command.ExpenseDate,
            cancellationToken);

        return updated is null
            ? Result.Failure<UpdateExpenseResult>(ExpenseErrors.NotFound)
            : Result.Success(UpdateExpenseResult.FromExpense(updated));
    }
}

public record UpdateExpenseResult(
    string Id,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt)
{
    public static UpdateExpenseResult FromExpense(Expense expense) => new(
        expense.Id,
        expense.Description,
        expense.AmountInCents,
        expense.Category.ToString(),
        expense.ExpenseDate,
        expense.CreatedAt);
}
