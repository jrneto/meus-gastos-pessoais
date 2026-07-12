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
    private const int MaxDescriptionLength = 200;

    private readonly IExpenseRepository _expenseRepository;

    public RegisterExpenseCommandHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result<RegisterExpenseResult>> Handle(RegisterExpenseCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Description))
            return Result.Failure<RegisterExpenseResult>(ExpenseErrors.Validation("Descrição é obrigatória."));
        if (command.Description.Length > MaxDescriptionLength)
            return Result.Failure<RegisterExpenseResult>(ExpenseErrors.Validation($"Descrição deve ter no máximo {MaxDescriptionLength} caracteres."));
        if (command.AmountInCents <= 0)
            return Result.Failure<RegisterExpenseResult>(ExpenseErrors.Validation("Valor deve ser maior que zero."));
        if (!Enum.TryParse<ExpenseCategory>(command.Category, ignoreCase: true, out var category) || !Enum.IsDefined(category))
            return Result.Failure<RegisterExpenseResult>(ExpenseErrors.Validation("Categoria inválida."));

        var expense = Expense.Create(
            command.UserId,
            command.Description,
            command.AmountInCents,
            category,
            command.ExpenseDate);

        await _expenseRepository.SaveAsync(expense, cancellationToken);

        return Result.Success(new RegisterExpenseResult(
            expense.Id,
            expense.Description,
            expense.AmountInCents,
            expense.Category.ToString(),
            expense.ExpenseDate,
            expense.CreatedAt));
    }
}

public record RegisterExpenseResult(
    string Id,
    string Description,
    long AmountInCents,
    string Category,
    DateOnly ExpenseDate,
    DateTimeOffset CreatedAt);
