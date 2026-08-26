using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterExpenseCommandValidatorTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly RegisterExpenseCommandValidator _validator;

    public RegisterExpenseCommandValidatorTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _categoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(Category.Restore("category-1", "user-id-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow));

        _validator = new RegisterExpenseCommandValidator(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionIsEmpty(string description)
    {
        var command = new RegisterExpenseCommand("user-id-123", description, 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionExceedsMaxLength()
    {
        var command = new RegisterExpenseCommand("user-id-123", new string('a', 201), 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Validate_ShouldBeInvalid_WhenAmountIsNotGreaterThanZero(long amountInCents)
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", amountInCents, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryIdIsEmpty()
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", 4590, "", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryDoesNotExist()
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", 4590, "category-inexistente", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryBelongsToAnotherUser()
    {
        _categoryRepositoryMock.GetByIdAsync("outro-user", "category-1", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var command = new RegisterExpenseCommand("outro-user", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }
}
