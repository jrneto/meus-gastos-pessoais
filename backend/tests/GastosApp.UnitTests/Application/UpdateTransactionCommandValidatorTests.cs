using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateTransactionCommandValidatorTests
{
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly UpdateTransactionCommandValidator _validator;

    public UpdateTransactionCommandValidatorTests()
    {
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _categoryRepositoryMock.GetByIdAsync("account-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(Category.Restore("category-1", "account-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow));

        _validator = new UpdateTransactionCommandValidator(_categoryRepositoryMock);
    }

    private static UpdateTransactionCommand Command(
        string accountId = "account-123",
        string description = "Almoço",
        long amountInCents = 4590,
        string categoryId = "category-1",
        string tipo = "despesa",
        DateOnly? date = null) =>
        new(accountId, "transaction-1", "user-123", MembershipRole.Total, description, amountInCents, categoryId, tipo, date ?? new DateOnly(2025, 6, 15));

    [Fact]
    public async Task Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var result = await _validator.ValidateAsync(Command());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionIsEmpty(string description)
    {
        var result = await _validator.ValidateAsync(Command(description: description));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenAmountIsNotGreaterThanZero()
    {
        var result = await _validator.ValidateAsync(Command(amountInCents: 0));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task Validate_ShouldBeInvalid_WhenTipoIsAbsentOrInvalid(string tipo)
    {
        var result = await _validator.ValidateAsync(Command(tipo: tipo));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryDoesNotExist()
    {
        var result = await _validator.ValidateAsync(Command(categoryId: "category-inexistente"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenCategoryBelongsToAnotherAccount()
    {
        _categoryRepositoryMock.GetByIdAsync("outra-conta", "category-1", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var result = await _validator.ValidateAsync(Command(accountId: "outra-conta"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenTipoDivergesFromCategoryTipo()
    {
        var result = await _validator.ValidateAsync(Command(tipo: "receita"));

        result.IsValid.Should().BeFalse();
    }
}
