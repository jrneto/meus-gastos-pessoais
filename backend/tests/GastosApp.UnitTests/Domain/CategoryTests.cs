using FluentAssertions;
using GastosApp.Domain.Categories;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_ShouldSetFieldsAndGenerateIdAndCreatedAt_WhenOrcamentoIsNull()
    {
        var category = Category.Create("account-1", "Viagem", "despesa", null);

        category.Id.Should().NotBeNullOrWhiteSpace();
        category.AccountId.Should().Be("account-1");
        category.Nome.Should().Be("Viagem");
        category.Tipo.Should().Be("despesa");
        category.OrcamentoMensalCents.Should().BeNull();
        category.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldSetOrcamentoMensalCents_WhenInformed()
    {
        var category = Category.Create("account-1", "Salario", "receita", 500000);

        category.Tipo.Should().Be("receita");
        category.OrcamentoMensalCents.Should().Be(500000);
    }

    [Fact]
    public void Restore_ShouldPreserveAllFields()
    {
        var createdAt = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

        var category = Category.Restore("category-1", "account-1", "Alimentacao", "despesa", 80000, createdAt);

        category.Id.Should().Be("category-1");
        category.AccountId.Should().Be("account-1");
        category.Nome.Should().Be("Alimentacao");
        category.Tipo.Should().Be("despesa");
        category.OrcamentoMensalCents.Should().Be(80000);
        category.CreatedAt.Should().Be(createdAt);
    }
}
