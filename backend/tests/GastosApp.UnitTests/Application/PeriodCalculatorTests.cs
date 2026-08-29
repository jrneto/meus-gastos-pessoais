using FluentAssertions;
using GastosApp.Application.Reports;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class PeriodCalculatorTests
{
    // ----- week -----

    [Fact]
    public void Calculate_ShouldReturnIsoWeek_WhenDateIsAWednesday()
    {
        // 2026-08-19 é uma quarta-feira; a semana ISO vai de 2026-08-17 (segunda) a 2026-08-23 (domingo).
        var date = new DateOnly(2026, 8, 19);

        var (current, previous) = PeriodCalculator.Calculate(date, "week");

        current.Start.Should().Be(new DateOnly(2026, 8, 17));
        current.End.Should().Be(new DateOnly(2026, 8, 23));
        previous.Start.Should().Be(new DateOnly(2026, 8, 10));
        previous.End.Should().Be(new DateOnly(2026, 8, 16));
    }

    [Fact]
    public void Calculate_ShouldResolveToSameWeek_WhenDateIsASunday()
    {
        // 2026-08-23 é domingo, último dia da mesma semana ISO de 2026-08-17 a 2026-08-23
        // — caso de borda de DayOfWeek.Sunday (valor 0 no enum .NET).
        var date = new DateOnly(2026, 8, 23);

        var (current, _) = PeriodCalculator.Calculate(date, "week");

        current.Start.Should().Be(new DateOnly(2026, 8, 17));
        current.End.Should().Be(new DateOnly(2026, 8, 23));
    }

    // ----- month -----

    [Fact]
    public void Calculate_ShouldReturnCalendarMonth_ForAnyDayInsideIt()
    {
        var date = new DateOnly(2026, 8, 15);

        var (current, previous) = PeriodCalculator.Calculate(date, "month");

        current.Start.Should().Be(new DateOnly(2026, 8, 1));
        current.End.Should().Be(new DateOnly(2026, 8, 31));
        previous.Start.Should().Be(new DateOnly(2026, 7, 1));
        previous.End.Should().Be(new DateOnly(2026, 7, 31));
    }

    [Fact]
    public void Calculate_ShouldRollBackToPreviousYear_WhenMonthIsJanuary()
    {
        var date = new DateOnly(2026, 1, 15);

        var (current, previous) = PeriodCalculator.Calculate(date, "month");

        current.Start.Should().Be(new DateOnly(2026, 1, 1));
        current.End.Should().Be(new DateOnly(2026, 1, 31));
        previous.Start.Should().Be(new DateOnly(2025, 12, 1));
        previous.End.Should().Be(new DateOnly(2025, 12, 31));
    }

    // ----- year -----

    [Fact]
    public void Calculate_ShouldReturnCalendarYear_ForAnyDayInsideIt()
    {
        var date = new DateOnly(2026, 8, 15);

        var (current, previous) = PeriodCalculator.Calculate(date, "year");

        current.Start.Should().Be(new DateOnly(2026, 1, 1));
        current.End.Should().Be(new DateOnly(2026, 12, 31));
        previous.Start.Should().Be(new DateOnly(2025, 1, 1));
        previous.End.Should().Be(new DateOnly(2025, 12, 31));
    }
}
