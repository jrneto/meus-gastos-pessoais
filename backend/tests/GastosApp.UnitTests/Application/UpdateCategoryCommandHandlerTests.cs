using FluentAssertions;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateCategoryCommandHandlerTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly UpdateCategoryCommandHandler _handler;

    public UpdateCategoryCommandHandlerTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _handler = new UpdateCategoryCommandHandler(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesCategory()
    {
        // Arrange
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "receita", 70000);
        var updated = Category.Restore("category-1", "user-id-123", "Viagens", "receita", 70000, DateTimeOffset.UtcNow);

        _categoryRepositoryMock.UpdateAsync(
                command.AccountId, command.CategoryId, command.Nome, command.Tipo, command.OrcamentoMensalCents, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Viagens");
        result.Value.Tipo.Should().Be("receita");
        result.Value.OrcamentoMensalCents.Should().Be(70000);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        // Arrange
        var command = new UpdateCategoryCommand("user-id-123", "category-inexistente", "Viagens", "despesa", null);

        _categoryRepositoryMock.UpdateAsync(
                command.AccountId, command.CategoryId, command.Nome, command.Tipo, command.OrcamentoMensalCents, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NotFound());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }

    [Fact]
    public async Task Handle_ShouldReturnNameConflict_WhenRenamingToExistingName()
    {
        // Arrange
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Lazer", "despesa", null);

        _categoryRepositoryMock.UpdateAsync(
                command.AccountId, command.CategoryId, command.Nome, command.Tipo, command.OrcamentoMensalCents, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.UnprocessableEntity);
        result.Error!.Code.Should().Be("name-conflict");
    }
}
