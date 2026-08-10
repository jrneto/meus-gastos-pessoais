using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Auth;

public static class AuthErrors
{
    public static Error EmailAlreadyExists => Error.Conflict("email-already-exists", "Email já cadastrado");

    public static Error InvalidCredentials => Error.Unauthorized("invalid-credentials", "Email ou senha inválidos");

    public static Error RefreshTokenMissing => Error.Unauthorized("refresh-token-missing", "Refresh token ausente.");

    public static Error InvalidRefreshToken => Error.Unauthorized("invalid-refresh-token", "Refresh token inválido ou expirado.");

    public static Error Validation(string message) => Error.Validation("bad-request", message);
}