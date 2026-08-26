using FluentValidation;

namespace GastosApp.Application.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(q => q.Tipo)
            .Must(tipo => tipo is null or "despesa" or "receita")
            .WithMessage("tipo deve ser \"despesa\" ou \"receita\".");
    }
}
