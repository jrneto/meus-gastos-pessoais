using System.Text.RegularExpressions;
using FluentValidation;

namespace GastosApp.Application.Summary.Queries.GetSummary;

public sealed partial class GetSummaryQueryValidator : AbstractValidator<GetSummaryQuery>
{
    public GetSummaryQueryValidator()
    {
        // Sem .When() aqui de propósito: por padrão o FluentValidation aplicaria
        // a condição a toda a cadeia (inclusive NotEmpty), deixando month vazio
        // passar sem erro — Matches() já rejeita string vazia sozinho, então
        // basta encadear as duas regras direto.
        RuleFor(q => q.Month)
            .NotEmpty().WithMessage("O parâmetro month é obrigatório.")
            .Matches(YearMonthRegex()).WithMessage("month deve estar no formato YYYY-MM.");
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex YearMonthRegex();
}
