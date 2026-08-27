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
        CancellationToken cancellationToken = default);
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
