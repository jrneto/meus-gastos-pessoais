namespace GastosApp.Api.Common;

// Lógica pura (sem HttpContext/Serilog direto) que decide o que entra na
// linha de log de uma requisição (FEAT-38) — extraída à parte pra ser
// testável por unit test, sem precisar interceptar o pipeline HTTP nem o
// Serilog de verdade (ver backend/tests/GastosApp.UnitTests/Api/
// RequestLogEntryBuilderTests.cs).
public static class RequestLogEntryBuilder
{
    // Corpo maior que isso é truncado antes de entrar no log, pra não
    // estourar custo/tamanho de evento do CloudWatch Logs com uma
    // resposta grande.
    public const int MaxLoggedBodyLength = 4096;

    // Protege contra um client mal-comportado inflando o log
    // indefinidamente via header — não rejeita a requisição, só trunca o
    // valor antes de logar/devolver (ver RequestObservabilityMiddleware).
    public const int MaxHeaderValueLength = 200;

    private const string TruncatedSuffix = "...(truncado)";

    public static IReadOnlyDictionary<string, object?> Build(
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? traceId,
        string? sessionId,
        string? clientPlatform,
        string? clientVersion,
        string? userId,
        bool fullPayloadLoggingEnabled,
        string? requestContentType,
        string? requestBody,
        string? responseContentType,
        string? responseBody)
    {
        var isError = statusCode >= 400;
        var shouldLogBody = isError || fullPayloadLoggingEnabled;

        var entry = new Dictionary<string, object?>
        {
            ["Method"] = method,
            ["Path"] = path,
            ["StatusCode"] = statusCode,
            ["DurationMs"] = durationMs,
            ["TraceId"] = traceId,
            ["SessionId"] = sessionId,
            ["ClientPlatform"] = clientPlatform,
            ["ClientVersion"] = clientVersion,
            ["UserId"] = userId
        };

        if (shouldLogBody)
        {
            entry["RequestBody"] = RedactIfJson(requestContentType, requestBody);
            entry["ResponseBody"] = RedactIfJson(responseContentType, responseBody);
        }

        return entry;
    }

    // Só corpo application/json (ou vazio) é candidato a log — exclui CSV
    // (GET /transactions/export), binários e qualquer outro content-type:
    // evita truncar/poluir o log com payload não-JSON, e não precisa de
    // regra de redação pra formato que a redação (JSON) não sabe
    // interpretar.
    private static string? RedactIfJson(string? contentType, string? body)
    {
        if (string.IsNullOrEmpty(body))
            return body;

        if (contentType is null || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            return null;

        var redacted = SensitiveFieldRedactor.Redact(body);

        return redacted.Length > MaxLoggedBodyLength
            ? redacted[..MaxLoggedBodyLength] + TruncatedSuffix
            : redacted;
    }
}
