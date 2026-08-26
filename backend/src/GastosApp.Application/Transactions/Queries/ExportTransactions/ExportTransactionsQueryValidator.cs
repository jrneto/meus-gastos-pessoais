using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;

namespace GastosApp.Application.Transactions.Queries.ExportTransactions;

public sealed partial class ExportTransactionsQueryValidator : AbstractValidator<ExportTransactionsQuery>
{
    private const string DateFormat = "yyyy-MM-dd";

    public ExportTransactionsQueryValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.Tipo)
            .Must(tipo => tipo is null or "despesa" or "receita")
            .WithMessage("tipo deve ser \"despesa\" ou \"receita\".");

        RuleFor(q => q.YearMonth)
            .Must(ym => ym is null || YearMonthRegex().IsMatch(ym))
            .WithMessage("yearMonth deve estar no formato YYYY-MM.");

        RuleFor(q => q.DateFrom)
            .Must(BeAValidDate)
            .WithMessage("dateFrom deve estar no formato YYYY-MM-DD.");

        RuleFor(q => q.DateTo)
            .Must(BeAValidDate)
            .WithMessage("dateTo deve estar no formato YYYY-MM-DD.");

        RuleFor(q => q)
            .Must(HaveConsistentDateRange)
            .WithMessage("dateFrom não pode ser posterior a dateTo.")
            .When(q => BeAValidDate(q.DateFrom) && BeAValidDate(q.DateTo) && q.DateFrom is not null && q.DateTo is not null);

        RuleFor(q => q.MinAmountInCents)
            .GreaterThan(0).WithMessage("minAmountInCents deve ser maior que zero.")
            .When(q => q.MinAmountInCents is not null);

        RuleFor(q => q.MaxAmountInCents)
            .GreaterThan(0).WithMessage("maxAmountInCents deve ser maior que zero.")
            .When(q => q.MaxAmountInCents is not null);

        RuleFor(q => q)
            .Must(q => q.MinAmountInCents!.Value <= q.MaxAmountInCents!.Value)
            .WithMessage("minAmountInCents não pode ser maior que maxAmountInCents.")
            .When(q => q.MinAmountInCents is not null && q.MaxAmountInCents is not null);
    }

    private static bool BeAValidDate(string? date) =>
        date is null || DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool HaveConsistentDateRange(ExportTransactionsQuery query) =>
        DateOnly.ParseExact(query.DateFrom!, DateFormat, CultureInfo.InvariantCulture)
            <= DateOnly.ParseExact(query.DateTo!, DateFormat, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonthRegex();
}
