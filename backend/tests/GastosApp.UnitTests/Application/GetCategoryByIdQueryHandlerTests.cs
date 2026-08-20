using FluentAssertions;
using GastosApp.Application.Categories.Queries.GetCategoryById;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetCategoryByIdQueryHandlerTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly GetCategoryByIdQueryHandler _handler;

    public GetCategoryByIdQueryHandlerTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _handler = new GetCategoryByIdQueryHandler(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryFindsCategory()
    {
        // Arrange
        var query = new GetCategoryByIdQuery("user-id-123", "category-1");

        var category = Category.Restore(
            "category-1", "user-id-123", "Alimentacao", "#F97316", "utensils", DateTimeOffset.UtcNow);

        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(category);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("category-1");
        result.Value.Nome.Should().Be("Alimentacao");
        result.Value.Cor.Should().Be("#F97316");
        result.Value.Icone.Should().Be("utensils");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRepositoryDoesNotFindCategory()
    {
        // Arrange
        var query = new GetCategoryByIdQuery("user-id-123", "category-inexistente");

        _categoryRepositoryMock.GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }
}
