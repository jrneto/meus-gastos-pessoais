using FluentAssertions;
using GastosApp.Application.Summary.Queries.GetSummary;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetSummaryQueryValidatorTests
{
    private readonly GetSummaryQueryValidator _validator = new();

    [Theory]
    [InlineData("")]
    public void Validate_ShouldBeInvalid_WhenMonthIsEmpty(string month)
    {
        var query = new GetSummaryQuery("account-123", "user-123", month);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("26-08")]
    [InlineData("2026/08")]
    [InlineData("2026-8")]
    [InlineData("agosto-2026")]
    public void Validate_ShouldBeInvalid_WhenMonthIsMalformed(string month)
    {
        var query = new GetSummaryQuery("account-123", "user-123", month);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-08")]
    [InlineData("2026-01")]
    [InlineData("2026-12")]
    public void Validate_ShouldBeValid_WhenMonthIsWellFormed(string month)
    {
        var query = new GetSummaryQuery("account-123", "user-123", month);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
