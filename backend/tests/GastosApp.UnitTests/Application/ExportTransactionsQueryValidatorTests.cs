using FluentAssertions;
using GastosApp.Application.Transactions.Queries.ExportTransactions;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class ExportTransactionsQueryValidatorTests
{
    private readonly ExportTransactionsQueryValidator _validator = new();

    private static ExportTransactionsQuery EmptyQuery => new(
        "account-123", "user-123", null, null, null, null, null, null, null);

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFiltersAreAbsent()
    {
        var result = _validator.Validate(EmptyQuery);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("despesa")]
    [InlineData("receita")]
    public void Validate_ShouldBeValid_WhenTipoIsDespesaOrReceita(string tipo)
    {
        var query = EmptyQuery with { Tipo = tipo };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public void Validate_ShouldBeInvalid_WhenTipoIsOutOfRange(string tipo)
    {
        var query = EmptyQuery with { Tipo = tipo };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2025-06")]
    [InlineData("2025-01")]
    [InlineData("2025-12")]
    public void Validate_ShouldBeValid_WhenYearMonthIsWellFormed(string yearMonth)
    {
        var query = EmptyQuery with { YearMonth = yearMonth };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2025-13")]
    [InlineData("2025-00")]
    [InlineData("25-06")]
    [InlineData("2025/06")]
    [InlineData("2025-6")]
    public void Validate_ShouldBeInvalid_WhenYearMonthIsMalformed(string yearMonth)
    {
        var query = EmptyQuery with { YearMonth = yearMonth };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenCategoryIdIsAnyNonEmptyString()
    {
        var query = EmptyQuery with { CategoryId = "qualquer-valor" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2025-06-15")]
    [InlineData("2025-01-01")]
    public void Validate_ShouldBeValid_WhenDatesAreWellFormed(string date)
    {
        var query = EmptyQuery with { DateFrom = date, DateTo = date };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2025/06/15")]
    [InlineData("15-06-2025")]
    [InlineData("2025-06-32")]
    [InlineData("not-a-date")]
    public void Validate_ShouldBeInvalid_WhenDateFromIsMalformed(string dateFrom)
    {
        var query = EmptyQuery with { DateFrom = dateFrom };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenDateFromIsAfterDateTo()
    {
        var query = EmptyQuery with { DateFrom = "2025-06-20", DateTo = "2025-06-10" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenDateFromEqualsDateTo()
    {
        var query = EmptyQuery with { DateFrom = "2025-06-10", DateTo = "2025-06-10" };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_ShouldBeInvalid_WhenMinAmountIsNotGreaterThanZero(long minAmount)
    {
        var query = EmptyQuery with { MinAmountInCents = minAmount };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_ShouldBeInvalid_WhenMaxAmountIsNotGreaterThanZero(long maxAmount)
    {
        var query = EmptyQuery with { MaxAmountInCents = maxAmount };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenMinAmountIsGreaterThanMaxAmount()
    {
        var query = EmptyQuery with { MinAmountInCents = 5000, MaxAmountInCents = 1000 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenAmountRangeIsConsistent()
    {
        var query = EmptyQuery with { MinAmountInCents = 1000, MaxAmountInCents = 5000 };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFiltersAreCombined()
    {
        var query = EmptyQuery with
        {
            Tipo = "despesa",
            CategoryId = "category-1",
            DateFrom = "2025-06-01",
            DateTo = "2025-06-30",
            MinAmountInCents = 1000,
            MaxAmountInCents = 5000
        };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
