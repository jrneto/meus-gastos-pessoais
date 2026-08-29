using FluentAssertions;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenOrcamentoMensalCentsIsInformed()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "receita", 60000);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenNomeIsEmpty(string nome)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", nome, "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNomeExceedsMaxLength()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", new string('a', 51), "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNomeHasNoAlphanumericCharacters()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "!!!", "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Receita")]
    [InlineData("income")]
    public void Validate_ShouldBeInvalid_WhenTipoIsNotDespesaOrReceita(string tipo)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", tipo, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldBeInvalid_WhenOrcamentoMensalCentsIsZeroOrNegative(long orcamentoMensalCents)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "despesa", orcamentoMensalCents);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
