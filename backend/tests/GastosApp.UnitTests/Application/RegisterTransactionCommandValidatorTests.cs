using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterTransactionCommandValidatorTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly RegisterTransactionCommandValidator _validator;

    public RegisterTransactionCommandValidatorTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _categoryRepositoryMock.GetByIdAsync("account-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(Category.Restore("category-1", "account-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow));
        _categoryRepositoryMock.GetByIdAsync("account-123", "category-receita", Arg.Any<CancellationToken>())
            .Returns(Category.Restore("category-receita", "account-123", "Salario", "receita", null, DateTimeOffset.UtcNow));

        _validator = new RegisterTransactionCommandValidator(_categoryRepositoryMock);
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenTipoIsReceitaAndCategoryMatches()
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Salário", 500000, "category-receita", "receita", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionIsEmpty(string description)
    {
        var command = new RegisterTransactionCommand(
            "account-123", description, 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionExceedsMaxLength()
    {
        var command = new RegisterTransactionCommand(
            "account-123", new string('a', 201), 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Validate_ShouldBeInvalid_WhenAmountIsNotGreaterThanZero(long amountInCents)
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", amountInCents, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task Validate_ShouldBeInvalid_WhenTipoIsAbsentOrInvalid(string tipo)
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", 4590, "category-1", tipo, new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryIdIsEmpty()
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", 4590, "", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryDoesNotExist()
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", 4590, "category-inexistente", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryBelongsToAnotherAccount()
    {
        _categoryRepositoryMock.GetByIdAsync("outra-conta", "category-1", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var command = new RegisterTransactionCommand(
            "outra-conta", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenTipoDivergesFromCategoryTipo()
    {
        // category-1 é "despesa" — tipo="receita" na transação diverge
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço", 4590, "category-1", "receita", new DateOnly(2025, 6, 15), "user-123");

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }
}
