namespace GastosApp.Api.Common;

// Centraliza as flags do cookie httpOnly de refresh token (FEAT-15) —
// usado por /auth/login (seta), /auth/refresh (limpa em falha) e
// /auth/logout (sempre limpa). Nunca trafega em JSON, só via Set-Cookie.
public static class RefreshTokenCookie
{
    public const string Name = "refreshToken";

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(5); // alinhado ao Cognito (FEAT-09)

    public static CookieOptions ForSet() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        MaxAge = MaxAge
    };

    public static CookieOptions ForClear() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        Expires = DateTimeOffset.UnixEpoch
    };
}