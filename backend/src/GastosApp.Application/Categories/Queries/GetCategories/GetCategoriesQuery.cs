using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using Mediator;

namespace GastosApp.Application.Categories.Queries.GetCategories;

public sealed record GetCategoriesQuery(string UserId) : IQuery<Result<GetCategoriesResult>>;

public sealed class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, Result<GetCategoriesResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<GetCategoriesResult>> Handle(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.ListAsync(query.UserId, cancellationToken);
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
    string Cor,
    string Icone,
    DateTimeOffset CreatedAt)
{
    public static CategorySummary FromEntity(Category category) => new(
        category.Id,
        category.Nome,
        category.Cor,
        category.Icone,
        category.CreatedAt);
}
