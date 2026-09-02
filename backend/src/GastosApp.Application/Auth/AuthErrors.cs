using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Auth;

public static class AuthErrors
{
    public static Error EmailAlreadyExists => Error.Conflict("email-already-exists", "Email já cadastrado");

    public static Error CpfAlreadyExists => Error.Conflict("cpf-already-exists", "CPF já cadastrado");

    public static Error InvalidCredentials => Error.Unauthorized("invalid-credentials", "Email ou senha inválidos");

    public static Error RefreshTokenMissing => Error.Unauthorized("refresh-token-missing", "Refresh token ausente.");

    public static Error InvalidRefreshToken => Error.Unauthorized("invalid-refresh-token", "Refresh token inválido ou expirado.");

    public static Error UserNotConfirmed => Error.Unauthorized("user-not-confirmed", "Usuário não confirmado. Por favor, confirme seu email antes de fazer login.");

    public static Error ProfileIncomplete => Error.Forbidden("profile-incomplete", "Cadastro incompleto. Este usuário não possui perfil (nome, telefone e CPF) cadastrado.");

    public static Error InvalidConfirmationCode => Error.Validation("invalid-confirmation-code", "Código de confirmação inválido.");

    public static Error ExpiredConfirmationCode => Error.Validation("expired-confirmation-code", "Código de confirmação expirado.");

    public static Error Validation(string message) => Error.Validation("bad-request", message);
}