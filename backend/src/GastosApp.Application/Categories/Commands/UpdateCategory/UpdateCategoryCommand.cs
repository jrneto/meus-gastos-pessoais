using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using Mediator;

namespace GastosApp.Application.Categories.Commands.UpdateCategory;

public sealed record UpdateCategoryCommand(
    string AccountId,
    string CategoryId,
    string Nome,
    string Cor,
    string Icone) : ICommand<Result<UpdateCategoryResult>>;

public sealed class UpdateCategoryCommandHandler : ICommandHandler<UpdateCategoryCommand, Result<UpdateCategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<UpdateCategoryResult>> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var result = await _categoryRepository.UpdateAsync(
            command.AccountId, command.CategoryId, command.Nome, command.Cor, command.Icone, cancellationToken);

        return result.Outcome switch
        {
            CategoryWriteOutcome.Success => Result.Success(UpdateCategoryResult.FromEntity(result.Category!)),
            CategoryWriteOutcome.NotFound => Result.Failure<UpdateCategoryResult>(CategoryErrors.NotFound),
            _ => Result.Failure<UpdateCategoryResult>(CategoryErrors.NameConflict)
        };
    }
}

public record UpdateCategoryResult(
    string Id,
    string Nome,
    string Cor,
    string Icone,
    DateTimeOffset CreatedAt)
{
    public static UpdateCategoryResult FromEntity(Category category) => new(
        category.Id,
        category.Nome,
        category.Cor,
        category.Icone,
        category.CreatedAt);
}
