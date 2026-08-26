using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using Mediator;

namespace GastosApp.Application.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery(string AccountId, string? Tipo) : IQuery<Result<GetCategoriesResult>>;

public sealed class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, Result<GetCategoriesResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<GetCategoriesResult>> Handle(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.ListAsync(query.AccountId, query.Tipo, cancellationToken);
        return Result.Success(GetCategoriesResult.FromEntities(categories));
    }
}

public sealed record GetCategoriesResult(IReadOnlyList<CategorySummary> Items)
{
    public static GetCategoriesResult FromEntities(IReadOnlyList<Category> categories) =>
        new(categories.Select(CategorySummary.FromEntity).ToList());
}

public sealed record CategorySummary(
    string Id,
    string Nome,
    string Tipo,
    long? OrcamentoMensalCents,
    DateTimeOffset CreatedAt)
{
    public static CategorySummary FromEntity(Category category) => new(
        category.Id,
        category.Nome,
        category.Tipo,
        category.OrcamentoMensalCents,
        category.CreatedAt);
}
