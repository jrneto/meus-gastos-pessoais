using FluentAssertions;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Expenses.Queries.GetExpenses;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetExpensesQueryValidatorTests
{
    private readonly GetExpensesQueryValidator _validator = new();

    private static GetExpensesQuery EmptyQuery => new(
        "user-id-123", null, null, null, null, null, null, null, null);

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFiltersAreAbsent()
    {
        var result = _validator.Validate(EmptyQuery);

        result.IsValid.Should().BeTrue();
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
        // Não há validação de formato/existência para o filtro categoryId (ver plan.md FEAT-17) —
        // um id que não existe simplesmente não retorna resultados, não é erro de validação.
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_ShouldBeInvalid_WhenLimitIsOutOfRange(int limit)
    {
        var query = EmptyQuery with { Limit = limit };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_ShouldBeValid_WhenLimitIsWithinRange(int limit)
    {
        var query = EmptyQuery with { Limit = limit };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-valid-cursor")]
    [InlineData("!!!invalid-base64!!!")]
    public void Validate_ShouldBeInvalid_WhenCursorCannotBeDecoded(string cursor)
    {
        var query = EmptyQuery with { Cursor = cursor };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenCursorIsDecodable()
    {
        var cursor = ExpenseCursorCodec.Encode(new ExpenseCursorPayload(
            "Base", new Dictionary<string, string> { ["PK"] = "ACCOUNT#user-id-123", ["SK"] = "TXN#2025-06-15#abc" }));
        var query = EmptyQuery with { Cursor = cursor };

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
