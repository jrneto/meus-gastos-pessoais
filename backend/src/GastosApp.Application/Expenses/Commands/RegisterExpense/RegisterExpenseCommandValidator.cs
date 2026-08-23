using FluentValidation;
using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Application.Expenses.Commands.RegisterExpense;

public sealed class RegisterExpenseCommandValidator : AbstractValidator<RegisterExpenseCommand>
{
    private const int MaxDescriptionLength = 200;

    private readonly ICategoryRepository _categoryRepository;

    public RegisterExpenseCommandValidator(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;

        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(MaxDescriptionLength).WithMessage($"Descrição deve ter no máximo {MaxDescriptionLength} caracteres.");

        RuleFor(c => c.AmountInCents)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

        RuleFor(c => c.CategoryId)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MustAsync(BeAnOwnedCategoryAsync).WithMessage("Categoria inválida.");
    }

    private async Task<bool> BeAnOwnedCategoryAsync(
        RegisterExpenseCommand command, string categoryId, CancellationToken cancellationToken) =>
        await _categoryRepository.GetByIdAsync(command.AccountId, categoryId, cancellationToken) is not null;
}
