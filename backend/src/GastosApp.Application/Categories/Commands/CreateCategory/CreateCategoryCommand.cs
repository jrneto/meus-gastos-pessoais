using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using Mediator;

namespace GastosApp.Application.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(
    string AccountId,
    string Nome,
    string Cor,
    string Icone) : ICommand<Result<CreateCategoryResult>>;

public sealed class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, Result<CreateCategoryResult>>
{
    private readonly ICategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async ValueTask<Result<CreateCategoryResult>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = Category.Create(command.AccountId, command.Nome, command.Cor, command.Icone);
        var result = await _categoryRepository.CreateAsync(category, cancellationToken);

        return result.Outcome switch
        {
            CategoryWriteOutcome.Success => Result.Success(CreateCategoryResult.FromEntity(result.Category!)),
            _ => Result.Failure<CreateCategoryResult>(CategoryErrors.NameConflict)
        };
    }
}

public record CreateCategoryResult(
    string Id,
    string Nome,
    string Cor,
    string Icone,
    DateTimeOffset CreatedAt)
{
    public static CreateCategoryResult FromEntity(Category category) => new(
        category.Id,
        category.Nome,
        category.Cor,
        category.Icone,
        category.CreatedAt);
}
