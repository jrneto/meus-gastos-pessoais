using FluentAssertions;
using GastosApp.Application.Categories.Commands.DeleteCategory;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class DeleteCategoryCommandHandlerTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly DeleteCategoryCommandHandler _handler;

    public DeleteCategoryCommandHandlerTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new DeleteCategoryCommandHandler(_categoryRepositoryMock, _expenseRepositoryMock);
    }

    private static Category SampleCategory(string nome = "Viagem") =>
        Category.Restore("category-1", "user-id-123", nome, "despesa", null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var command = new DeleteCategoryCommand("user-id-123", "category-inexistente");
        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-inexistente", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("not-found");

        await _expenseRepositoryMock.DidNotReceiveWithAnyArgs().ExistsByCategoryAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnCategoryInUse_WhenExpensesReferenceCategoryId()
    {
        // Arrange
        var command = new DeleteCategoryCommand("user-id-123", "category-1");
        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory("Alimentacao"));
        _expenseRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.UnprocessableEntity);
        result.Error!.Code.Should().Be("category-in-use");

        await _categoryRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldDeleteCategory_WhenNoExpensesReferenceIt()
    {
        // Arrange
        var command = new DeleteCategoryCommand("user-id-123", "category-1");
        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory("Viagem"));
        _expenseRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepositoryMock.DeleteAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        Received.InOrder(() =>
        {
            _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>());
            _expenseRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>());
            _categoryRepositoryMock.DeleteAsync("user-id-123", "category-1", Arg.Any<CancellationToken>());
        });
    }
}
