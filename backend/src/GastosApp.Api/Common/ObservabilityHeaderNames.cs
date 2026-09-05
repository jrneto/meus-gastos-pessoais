namespace GastosApp.Api.Common;

// Nomes dos 4 headers de observabilidade (FEAT-38) — minúsculo, separados
// por traço, sem prefixo X- (RFC 6648 + padrão de fato em HTTP/2+, ver
// backend/specs/FEAT-38-observabilidade-headers-api/spec.md).
public static class ObservabilityHeaderNames
{
    public const string TraceId = "trace-id";
    public const string SessionId = "session-id";
    public const string ClientPlatform = "client-platform";
    public const string ClientVersion = "client-version";
}
