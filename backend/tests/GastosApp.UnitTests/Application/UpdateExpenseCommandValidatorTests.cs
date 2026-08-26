using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateExpenseCommandValidatorTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly UpdateExpenseCommandValidator _validator;

    public UpdateExpenseCommandValidatorTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(Category.Restore("category-1", "user-id-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow));

        _validator = new UpdateExpenseCommandValidator(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new UpdateExpenseCommand("user-id-123", "expense-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionIsEmpty(string description)
    {
        var command = new UpdateExpenseCommand("user-id-123", "expense-1", description, 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenAmountIsNotGreaterThanZero()
    {
        var command = new UpdateExpenseCommand("user-id-123", "expense-1", "Almoço", 0, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryDoesNotExist()
    {
        var command = new UpdateExpenseCommand("user-id-123", "expense-1", "Almoço", 4590, "category-inexistente", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryBelongsToAnotherUser()
    {
        _categoryRepositoryMock.GetByIdAsync("outro-user", "category-1", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var command = new UpdateExpenseCommand("outro-user", "expense-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }
}
