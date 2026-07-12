using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Expenses;

public static class ExpenseErrors
{
    public static Error Validation(string message) => Error.Validation("validation-error", message);
}
