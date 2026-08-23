using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Accounts;

public static class AccountErrors
{
    public static Error NotResolved =>
        Error.Unauthorized("account-not-found", "Conta não encontrada para este usuário.");
}
