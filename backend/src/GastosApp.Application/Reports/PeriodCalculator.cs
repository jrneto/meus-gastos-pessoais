using System.Globalization;

namespace GastosApp.Application.Reports;

public readonly record struct PeriodRange(DateOnly Start, DateOnly End);

// Função pura — toda entrada vem do parâmetro `date` já validado pelo
// GetReportsQueryValidator (spec.md, decisão de escopo 1: sem default de
// "hoje", nunca depende do relógio do servidor).
public static class PeriodCalculator
{
    public static (PeriodRange Current, PeriodRange Previous) Calculate(DateOnly date, string period) =>
        period switch
        {
            "week" => CalculateWeek(date),
            "month" => CalculateMonth(date),
            "year" => CalculateYear(date),
            // Inalcançável em produção: `period` já chega validado pelo
            // ValidationBehavior (só "week"/"month"/"year" passam).
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "period deve ser week, month ou year.")
        };

    private static (PeriodRange, PeriodRange) CalculateWeek(DateOnly date)
    {
        // ISOWeek (System.Globalization) resolve a segunda-feira da semana ISO
        // diretamente, sem aritmética manual sobre DayOfWeek — evita o caso de
        // borda de DayOfWeek.Sunday valer 0.
        var isoYear = ISOWeek.GetYear(date);
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        var monday = ISOWeek.ToDateOnly(isoYear, isoWeek, DayOfWeek.Monday);

        var current = new PeriodRange(monday, monday.AddDays(6));
        var previous = new PeriodRange(monday.AddDays(-7), monday.AddDays(-1));
        return (current, previous);
    }

    private static (PeriodRange, PeriodRange) CalculateMonth(DateOnly date)
    {
        var firstDay = new DateOnly(date.Year, date.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var current = new PeriodRange(firstDay, lastDay);

        var previousFirstDay = firstDay.AddMonths(-1);
        var previous = new PeriodRange(previousFirstDay, firstDay.AddDays(-1));
        return (current, previous);
    }

    private static (PeriodRange, PeriodRange) CalculateYear(DateOnly date)
    {
        var current = new PeriodRange(new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31));
        var previous = new PeriodRange(new DateOnly(date.Year - 1, 1, 1), new DateOnly(date.Year - 1, 12, 31));
        return (current, previous);
    }
}
