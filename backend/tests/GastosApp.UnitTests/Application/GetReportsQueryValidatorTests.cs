using FluentAssertions;
using GastosApp.Application.Reports.Queries.GetReports;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetReportsQueryValidatorTests
{
    private readonly GetReportsQueryValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("dia")]
    [InlineData("Week")]
    public void Validate_ShouldBeInvalid_WhenPeriodIsEmptyOrNotOneOfTheAllowedValues(string period)
    {
        var query = new GetReportsQuery("account-123", period, "2026-08-15");

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("year")]
    public void Validate_ShouldBeValid_WhenPeriodIsOneOfTheAllowedValues(string period)
    {
        var query = new GetReportsQuery("account-123", period, "2026-08-15");

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    public void Validate_ShouldBeInvalid_WhenDateIsEmpty(string date)
    {
        var query = new GetReportsQuery("account-123", "month", date);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026/08/15")]
    [InlineData("15-08-2026")]
    [InlineData("agosto")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    public void Validate_ShouldBeInvalid_WhenDateIsMalformedOrNotAValidCalendarDate(string date)
    {
        var query = new GetReportsQuery("account-123", "month", date);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenPeriodAndDateAreWellFormed()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
