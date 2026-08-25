using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Members;

public static class MembershipErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Membro não encontrado.");

    public static Error AlreadyExists =>
        Error.Conflict("member-already-exists", "Este e-mail já é membro desta conta.");

    public static Error CannotModifyTitular => Error.UnprocessableEntity(
        "cannot-modify-titular", "O Titular da conta não pode ter o papel alterado.");

    public static Error CannotRemoveTitular => Error.UnprocessableEntity(
        "cannot-remove-titular", "O Titular da conta não pode ser removido.");

    public static Error InsufficientPermission => Error.Forbidden(
        "insufficient-permission", "Seu nível de acesso não permite esta ação.");
}
