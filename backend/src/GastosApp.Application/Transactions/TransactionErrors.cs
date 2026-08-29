using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Transactions;

public static class TransactionErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Transação não encontrada.");
}
