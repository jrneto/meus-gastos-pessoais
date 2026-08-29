using FluentAssertions;
using GastosApp.Application.Categories.Commands.CreateCategory;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new CreateCategoryCommand("user-id-123", "Viagem", "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenOrcamentoMensalCentsIsInformed()
    {
        var command = new CreateCategoryCommand("user-id-123", "Viagem", "receita", 50000);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenNomeIsEmpty(string nome)
    {
        var command = new CreateCategoryCommand("user-id-123", nome, "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNomeExceedsMaxLength()
    {
        var command = new CreateCategoryCommand("user-id-123", new string('a', 51), "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("🎉🎉🎉")]
    public void Validate_ShouldBeInvalid_WhenNomeHasNoAlphanumericCharacters(string nome)
    {
        var command = new CreateCategoryCommand("user-id-123", nome, "despesa", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Despesa")]
    [InlineData("DESPESA")]
    [InlineData("expense")]
    public void Validate_ShouldBeInvalid_WhenTipoIsNotDespesaOrReceita(string tipo)
    {
        var command = new CreateCategoryCommand("user-id-123", "Viagem", tipo, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Validate_ShouldBeInvalid_WhenOrcamentoMensalCentsIsZeroOrNegative(long orcamentoMensalCents)
    {
        var command = new CreateCategoryCommand("user-id-123", "Viagem", "despesa", orcamentoMensalCents);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
