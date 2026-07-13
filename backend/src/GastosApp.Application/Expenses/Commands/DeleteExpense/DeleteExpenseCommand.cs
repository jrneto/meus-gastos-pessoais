using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Expenses.Commands.DeleteExpense;

public sealed record DeleteExpenseCommand(string UserId, string ExpenseId) : ICommand<Result>;

public sealed class DeleteExpenseCommandHandler : ICommandHandler<DeleteExpenseCommand, Result>
{
    private readonly IExpenseRepository _expenseRepository;

    public DeleteExpenseCommandHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result> Handle(DeleteExpenseCommand command, CancellationToken cancellationToken)
    {
        var deleted = await _expenseRepository.DeleteAsync(command.UserId, command.ExpenseId, cancellationToken);

        return deleted ? Result.Success() : Result.Failure(ExpenseErrors.NotFound);
    }
}
