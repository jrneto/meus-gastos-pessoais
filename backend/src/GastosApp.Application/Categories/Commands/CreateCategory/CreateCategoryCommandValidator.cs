using FluentValidation;
using GastosApp.Domain.Categories;

namespace GastosApp.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNomeLength = 50;

    public CreateCategoryCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(MaxNomeLength).WithMessage($"Nome deve ter no máximo {MaxNomeLength} caracteres.")
            .Must(nome => CategorySlug.From(nome).Length > 0)
                .WithMessage("Nome deve conter ao menos uma letra ou número.");

        RuleFor(c => c.Tipo)
            .NotEmpty().WithMessage("Tipo é obrigatório.")
            .Must(tipo => tipo is "despesa" or "receita")
                .WithMessage("Tipo deve ser \"despesa\" ou \"receita\".");

        RuleFor(c => c.OrcamentoMensalCents)
            .GreaterThan(0).WithMessage("Orçamento mensal deve ser um valor positivo em centavos.")
            .When(c => c.OrcamentoMensalCents is not null);
    }
}
