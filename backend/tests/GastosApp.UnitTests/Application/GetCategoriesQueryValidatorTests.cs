using FluentAssertions;
using GastosApp.Application.Categories.Queries.GetCategories;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetCategoriesQueryValidatorTests
{
    private readonly GetCategoriesQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldBeValid_WhenTipoIsNull()
    {
        var query = new GetCategoriesQuery("user-id-123", null);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("despesa")]
    [InlineData("receita")]
    public void Validate_ShouldBeValid_WhenTipoIsDespesaOrReceita(string tipo)
    {
        var query = new GetCategoriesQuery("user-id-123", tipo);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("Despesa")]
    public void Validate_ShouldBeInvalid_WhenTipoIsNotDespesaOrReceita(string tipo)
    {
        var query = new GetCategoriesQuery("user-id-123", tipo);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }
}
