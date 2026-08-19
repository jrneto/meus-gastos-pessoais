using FluentAssertions;
using GastosApp.Application.Categories.Queries.GetCategories;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetCategoriesQueryHandlerTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly GetCategoriesQueryHandler _handler;

    public GetCategoriesQueryHandlerTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _handler = new GetCategoriesQueryHandler(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyItems_WhenUserHasNoCategories()
    {
        // Arrange
        var query = new GetCategoriesQuery("user-id-123");
        _categoryRepositoryMock.ListAsync("user-id-123", Arg.Any<CancellationToken>())
            .Returns(new List<Category>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldMapCategories_WhenUserHasCategories()
    {
        // Arrange
        var query = new GetCategoriesQuery("user-id-123");
        var category = Category.Restore("category-1", "user-id-123", "Viagem", "#0EA5E9", "plane", DateTimeOffset.UtcNow);

        _categoryRepositoryMock.ListAsync("user-id-123", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { category });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be("category-1");
        result.Value.Items[0].Nome.Should().Be("Viagem");
        result.Value.Items[0].Cor.Should().Be("#0EA5E9");
        result.Value.Items[0].Icone.Should().Be("plane");
    }
}
