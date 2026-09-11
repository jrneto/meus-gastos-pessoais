namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Abstrai como uma requisição HTTP chega até a API sob teste — a mesma
/// suíte roda contra dois transportes fisicamente diferentes:
/// <see cref="DirectHttpTransport"/> (Hom/Prod, HTTPS normal via API
/// Gateway) e <see cref="LambdaRieTransport"/> (Local, protocolo de
/// invocação do Lambda Runtime Interface Emulator, contra o binário
/// Native AOT publicado). Ver plan.md, "Abstração de transporte HTTP".
/// </summary>
public interface IApiTransport : IDisposable
{
    Task<TransportResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearerToken = null,
        // FEAT-39: trace-id/client-platform/client-version são enviados
        // por padrão em toda chamada (ver ObservabilityHeaderDefaults) —
        // sem isso, praticamente toda a suíte integrada seria rejeitada
        // com 400 antes de chegar na lógica de negócio. Um teste que
        // precisa simular a ausência de algum desses headers passa o(s)
        // nome(s) aqui (ver ObservabilityFlowTests).
        IReadOnlyCollection<string>? omitObservabilityHeaders = null,
        CancellationToken cancellationToken = default);
}

// Headers de observabilidade (backend/specs/FEAT-38 e FEAT-39) enviados
// por padrão por todo transporte — mesmo espírito do frontend web
// (frontend/app/src/lib/httpClient.ts), sem depender de um
// ProjectReference a GastosApp.Api (suíte é black-box de propósito).
internal static class ObservabilityHeaderDefaults
{
    public const string TraceId = "trace-id";
    public const string ClientPlatform = "client-platform";
    public const string ClientVersion = "client-version";

    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        [TraceId] = "integration-tests-trace-id",
        [ClientPlatform] = "integration-tests",
        [ClientVersion] = "0.0.0-test"
    };
}

public sealed record TransportResponse(
    int StatusCode,
    string Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public T Deserialize<T>() =>
        System.Text.Json.JsonSerializer.Deserialize<T>(Body, JsonDefaults.Options)
        ?? throw new InvalidOperationException($"Corpo da resposta não pôde ser desserializado para {typeof(T).Name}: {Body}");
}

internal static class JsonDefaults
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
