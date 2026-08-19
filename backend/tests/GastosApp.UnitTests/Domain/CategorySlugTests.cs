using FluentAssertions;
using GastosApp.Domain.Categories;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class CategorySlugTests
{
    [Fact]
    public void From_ShouldRemoveDiacriticsAndReplaceSpacesWithHyphen()
    {
        CategorySlug.From("Compras e Serviços").Should().Be("compras-e-servicos");
    }

    [Theory]
    [InlineData("Lazer")]
    [InlineData("  lazer  ")]
    [InlineData("LAZER")]
    public void From_ShouldProduceSameSlug_RegardlessOfCaseOrSurroundingSpaces(string nome)
    {
        CategorySlug.From(nome).Should().Be("lazer");
    }

    [Fact]
    public void From_ShouldCollapseRepeatedSpaces_IntoSingleHyphen()
    {
        CategorySlug.From("Compras  e  Serviços").Should().Be(CategorySlug.From("Compras e Serviços"));
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("???")]
    [InlineData("🎉🎉🎉")]
    public void From_ShouldReturnEmptyString_WhenNameHasNoAlphanumericCharacters(string nome)
    {
        CategorySlug.From(nome).Should().BeEmpty();
    }
}
