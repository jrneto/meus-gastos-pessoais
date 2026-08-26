using FluentValidation;
using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Application.Transactions.Commands.RegisterTransaction;

public sealed class RegisterTransactionCommandValidator : AbstractValidator<RegisterTransactionCommand>
{
    private const int MaxDescriptionLength = 200;

    private readonly ICategoryRepository _categoryRepository;

    public RegisterTransactionCommandValidator(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;

        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(MaxDescriptionLength).WithMessage($"Descrição deve ter no máximo {MaxDescriptionLength} caracteres.");

        RuleFor(c => c.AmountInCents)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

        RuleFor(c => c.Tipo)
            .NotEmpty().WithMessage("Tipo é obrigatório.")
            .Must(tipo => tipo is "despesa" or "receita").WithMessage("Tipo deve ser \"despesa\" ou \"receita\".");

        RuleFor(c => c.CategoryId)
            .NotEmpty().WithMessage("Categoria é obrigatória.")
            .MustAsync(BeAnOwnedCategoryOfMatchingTypeAsync).WithMessage("Categoria inválida.");
    }

    // Uma só chamada ao repositório cobre os três casos que a spec trata como
    // indistinguíveis (inexistente / de outra conta / tipo divergente) — mesmo
    // padrão de "não vazar detalhe" já usado pela FEAT-17 pra categoria de outro usuário.
    private async Task<bool> BeAnOwnedCategoryOfMatchingTypeAsync(
        RegisterTransactionCommand command, string categoryId, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(command.AccountId, categoryId, cancellationToken);
        return category is not null && category.Tipo == command.Tipo;
    }
}
