using System.Globalization;
using FluentValidation;

namespace GastosApp.Application.Reports.Queries.GetReports;

public sealed class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    private const string DateFormat = "yyyy-MM-dd";

    public GetReportsQueryValidator()
    {
        RuleFor(q => q.Period)
            .Must(p => p is "week" or "month" or "year")
            .WithMessage("O parâmetro period é obrigatório e deve ser week, month ou year.");

        // Sem .When() de propósito: encadeado no fim da regra ele se aplicaria a toda a
        // cadeia anterior, inclusive NotEmpty() — bug real já corrigido na FEAT-23
        // (GetSummaryQueryValidator). Aqui não é necessário: Must(BeAValidDate) já rejeita
        // string vazia sozinho (DateOnly.TryParseExact("", ...) retorna false).
        RuleFor(q => q.Date)
            .NotEmpty().WithMessage("O parâmetro date é obrigatório.")
            .Must(BeAValidDate).WithMessage("date deve estar no formato YYYY-MM-DD.");
    }

    private static bool BeAValidDate(string date) =>
        DateOnly.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
