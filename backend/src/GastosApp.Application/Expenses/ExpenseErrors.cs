using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Expenses;

public static class ExpenseErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Despesa não encontrada.");
}
