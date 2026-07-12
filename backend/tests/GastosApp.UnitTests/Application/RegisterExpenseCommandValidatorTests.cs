using FluentAssertions;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterExpenseCommandValidatorTests
{
    private readonly RegisterExpenseCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", 4590, "Alimentacao", new DateOnly(2025, 6, 15));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenDescriptionIsEmpty(string description)
    {
        var command = new RegisterExpenseCommand("user-id-123", description, 4590, "Alimentacao", new DateOnly(2025, 6, 15));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenDescriptionExceedsMaxLength()
    {
        var command = new RegisterExpenseCommand("user-id-123", new string('a', 201), 4590, "Alimentacao", new DateOnly(2025, 6, 15));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_ShouldBeInvalid_WhenAmountIsNotGreaterThanZero(long amountInCents)
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", amountInCents, "Alimentacao", new DateOnly(2025, 6, 15));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenCategoryIsNotDefined()
    {
        var command = new RegisterExpenseCommand("user-id-123", "Almoço", 4590, "CategoriaInexistente", new DateOnly(2025, 6, 15));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
