using FluentValidation;
using GastosApp.Domain.Expenses;

namespace GastosApp.Application.Expenses.Commands.UpdateExpense;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    private const int MaxDescriptionLength = 200;

    public UpdateExpenseCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(MaxDescriptionLength).WithMessage($"Descrição deve ter no máximo {MaxDescriptionLength} caracteres.");

        RuleFor(c => c.AmountInCents)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

        RuleFor(c => c.Category)
            .Must(BeAValidCategory).WithMessage("Categoria inválida.");
    }

    private static bool BeAValidCategory(string category) =>
        Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
