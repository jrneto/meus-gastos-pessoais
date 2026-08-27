using FluentAssertions;
using GastosApp.Domain.Categories;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class DefaultCategorySeedTests
{
    [Fact]
    public void Items_ShouldHaveExactlyThirteenEntries()
    {
        DefaultCategorySeed.Items.Should().HaveCount(13);
    }

    [Fact]
    public void Items_ShouldAllHaveValidAndDistinctGuidIds()
    {
        var ids = DefaultCategorySeed.Items.Select(item => item.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
        ids.Should().OnlyContain(id => IsValidGuid(id));
    }

    [Fact]
    public void Items_ShouldAllHaveDistinctNames()
    {
        var nomes = DefaultCategorySeed.Items.Select(item => item.Nome).ToList();

        nomes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Items_ShouldAllHaveDistinctSlugs()
    {
        // Garante que nenhum par de nomes colide no mesmo SK (CAT#<slug>) —
        // ex.: dois nomes diferentes que normalizam pro mesmo slug quebrariam
        // a unicidade condicional do PutItem em DynamoDbAccountRepository.
        var slugs = DefaultCategorySeed.Items.Select(item => CategorySlug.From(item.Nome)).ToList();

        slugs.Should().OnlyHaveUniqueItems();
        slugs.Should().OnlyContain(slug => !string.IsNullOrEmpty(slug));
    }

    [Fact]
    public void Tipo_ShouldBeDespesa()
    {
        DefaultCategorySeed.Tipo.Should().Be("despesa");
    }

    private static bool IsValidGuid(string value) => Guid.TryParse(value, out _);
}
