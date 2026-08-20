using System.Text.RegularExpressions;
using FluentValidation;
using GastosApp.Domain.Categories;

namespace GastosApp.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    private const int MaxNomeLength = 50;
    private const int MaxIconeLength = 50;
    private static readonly Regex HexColor = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public CreateCategoryCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(MaxNomeLength).WithMessage($"Nome deve ter no máximo {MaxNomeLength} caracteres.")
            .Must(nome => CategorySlug.From(nome).Length > 0)
                .WithMessage("Nome deve conter ao menos uma letra ou número.");

        RuleFor(c => c.Cor)
            .NotEmpty().WithMessage("Cor é obrigatória.")
            .Matches(HexColor).WithMessage("Cor deve estar no formato hexadecimal #RRGGBB.");

        RuleFor(c => c.Icone)
            .NotEmpty().WithMessage("Ícone é obrigatório.")
            .MaximumLength(MaxIconeLength).WithMessage($"Ícone deve ter no máximo {MaxIconeLength} caracteres.");
    }
}
