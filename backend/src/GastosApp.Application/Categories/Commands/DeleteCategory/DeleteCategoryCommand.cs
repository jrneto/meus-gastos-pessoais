using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Categories.Commands.DeleteCategory;

public sealed record DeleteCategoryCommand(string AccountId, string CategoryId) : ICommand<Result>;

public sealed class DeleteCategoryCommandHandler : ICommandHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IExpenseRepository _expenseRepository;

    public DeleteCategoryCommandHandler(ICategoryRepository categoryRepository, IExpenseRepository expenseRepository)
    {
        _categoryRepository = categoryRepository;
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(command.AccountId, command.CategoryId, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.NotFound);

        var inUse = await _expenseRepository.ExistsByCategoryAsync(command.AccountId, command.CategoryId, cancellationToken);
        if (inUse)
            return Result.Failure(CategoryErrors.CategoryInUse);

        var deleted = await _categoryRepository.DeleteAsync(command.AccountId, command.CategoryId, cancellationToken);
        return deleted ? Result.Success() : Result.Failure(CategoryErrors.NotFound);
    }
}
