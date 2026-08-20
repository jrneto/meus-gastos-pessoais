using GastosApp.Application.Categories;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Categories.Queries.GetCategoryById;

public sealed record GetCategoryByIdQuery(string UserId, string CategoryId) : IQuery<Result<UpdateCategoryResult>>;

public sealed class GetCategoryByIdQueryHandler : IQueryHandler<GetCategoryByIdQuery, Result<UpdateCategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryByIdQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<UpdateCategoryResult>> Handle(GetCategoryByIdQuery query, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(query.UserId, query.CategoryId, cancellationToken);

        return category is null
            ? Result.Failure<UpdateCategoryResult>(CategoryErrors.NotFound)
            : Result.Success(UpdateCategoryResult.FromEntity(category));
    }
}
