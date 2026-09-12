using GastosApp.Application.Common.Results;
using Microsoft.AspNetCore.Http;

namespace GastosApp.Api.Common;

// Lógica pura (sem HttpContext/Serilog direto) que decide se uma rota é
// isenta da obrigatoriedade dos headers de observabilidade e quais deles
// estão ausentes numa requisição (FEAT-39) — extraída à parte pra ser
// testável por unit test, mesmo espírito de RequestLogEntryBuilder.
public static class ObservabilityHeaderValidator
{
    // GET /health fica isento da obrigatoriedade dos 3 headers — continua
    // consultável via curl manual simples (checagem manual de versão em
    // produção, sem exigir login nem headers customizados), ver
    // HealthEndpoints.cs.
    private static readonly PathString HealthPath = "/health";

    public static bool IsExemptPath(PathString path) =>
        path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase);

    // Devolve os nomes dos headers obrigatórios ausentes (lista vazia se
    // nenhum), na ordem trace-id/client-platform/client-version. Recebe
    // os valores já truncados (mesmo Truncate() do middleware) — vazio ou
    // nulo conta como ausente. session-id não entra aqui: continua
    // totalmente opcional (ver spec.md, "Fora do escopo").
    public static IReadOnlyList<string> GetMissingHeaders(
        string? traceId, string? clientPlatform, string? clientVersion)
    {
        var missing = new List<string>(3);

        if (string.IsNullOrEmpty(traceId))
            missing.Add(ObservabilityHeaderNames.TraceId);

        if (string.IsNullOrEmpty(clientPlatform))
            missing.Add(ObservabilityHeaderNames.ClientPlatform);

        if (string.IsNullOrEmpty(clientVersion))
            missing.Add(ObservabilityHeaderNames.ClientVersion);

        return missing;
    }

    // Mensagem única, reaproveitando o mesmo Error/ProblemDetails de
    // qualquer outra validação do projeto (ver ResultHttpExtensions) —
    // detail lista todos os headers ausentes, não só o primeiro.
    public static Error BuildMissingHeadersError(IReadOnlyList<string> missingHeaders) =>
        Error.Validation(
            "missing-observability-headers",
            $"Header(s) obrigatório(s) ausente(s): {string.Join(", ", missingHeaders)}");
}
