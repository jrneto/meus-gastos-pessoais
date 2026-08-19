using FluentAssertions;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class CreateCategoryCommandHandlerTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly CreateCategoryCommandHandler _handler;

    public CreateCategoryCommandHandlerTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _handler = new CreateCategoryCommandHandler(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryCreatesCategory()
    {
        // Arrange
        var command = new CreateCategoryCommand("user-id-123", "Viagem", "#0EA5E9", "plane");

        _categoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(call => CategoryWriteResult.Success(call.Arg<Category>()));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Viagem");
        result.Value.Cor.Should().Be("#0EA5E9");
        result.Value.Icone.Should().Be("plane");
        result.Value.Id.Should().NotBeNullOrWhiteSpace();

        await _categoryRepositoryMock.Received(1).CreateAsync(
            Arg.Is<Category>(c => c.UserId == command.UserId && c.Nome == command.Nome),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNameConflict_WhenRepositoryReportsDuplicateName()
    {
        // Arrange
        var command = new CreateCategoryCommand("user-id-123", "Lazer", "#0EA5E9", "plane");

        _categoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.UnprocessableEntity);
        result.Error!.Code.Should().Be("name-conflict");
    }
}
