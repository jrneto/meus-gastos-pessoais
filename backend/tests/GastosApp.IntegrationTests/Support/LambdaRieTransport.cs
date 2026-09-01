using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Transporte usado contra o ambiente Local — não fala HTTP com a API
/// diretamente (o container não expõe Kestrel), fala o protocolo de
/// invocação do Lambda Runtime Interface Emulator (RIE), que é o mesmo
/// caminho por onde o API Gateway invocaria a Lambda real. Monta um
/// evento no payload format 2.0 (mesmo formato configurado em produção,
/// <c>LambdaEventSource.HttpApi</c> — <c>Program.cs</c>), envia pro RIE,
/// e desempacota a resposta de volta pra <see cref="TransportResponse"/>.
///
/// É esse caminho — binário Native AOT publicado, invocado como a
/// Lambda real seria — que expõe erro específico de AOT (reflection não
/// suportada, `services.Configure&lt;T&gt;()` silenciosamente incorreto
/// etc.) antes de qualquer deploy real (ver spec.md, US1).
/// </summary>
public sealed class LambdaRieTransport : IApiTransport, IDisposable
{
    private readonly HttpClient _rieClient;
    private readonly string _invokeUrl;

    public LambdaRieTransport(string invokeUrl)
    {
        _invokeUrl = invokeUrl;
        // Timeout generoso: primeiro invoke depois do container subir
        // sofre cold start real do host Native AOT.
        _rieClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<TransportResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };
        if (!string.IsNullOrEmpty(bearerToken))
            headers["authorization"] = $"Bearer {bearerToken}";

        string? requestBody = body is null ? null : JsonSerializer.Serialize(body, JsonDefaults.Options);

        // Achado real (FEAT-32): "path" pode conter query string (ex.:
        // "/summary?month=2026-08") — precisa ser separado em rawPath +
        // rawQueryString antes de montar o evento do API Gateway v2,
        // senão o roteamento da Api recebe o "?..." como parte literal do
        // path e nunca casa nenhuma rota (404). Nunca apareceu antes
        // porque nenhum teste do módulo Auth (único coberto até a
        // FEAT-29) usava query string em modo local.
        var queryStringIndex = path.IndexOf('?', StringComparison.Ordinal);
        var rawPath = queryStringIndex >= 0 ? path[..queryStringIndex] : path;
        var rawQueryString = queryStringIndex >= 0 ? path[(queryStringIndex + 1)..] : "";

        var httpMethod = method.Method;
        var evt = new ApiGatewayV2Request
        {
            Version = "2.0",
            RouteKey = "$default",
            RawPath = rawPath,
            RawQueryString = rawQueryString,
            Headers = headers,
            RequestContext = new ApiGatewayV2RequestContext
            {
                Http = new ApiGatewayV2Http { Method = httpMethod, Path = rawPath }
            },
            Body = requestBody,
            IsBase64Encoded = false
        };

        var eventJson = JsonSerializer.Serialize(evt, RieJsonContext.Default.ApiGatewayV2Request);

        using var invokeRequest = new HttpRequestMessage(HttpMethod.Post, _invokeUrl)
        {
            Content = new StringContent(eventJson, Encoding.UTF8, "application/json")
        };

        using var invokeResponse = await _rieClient.SendAsync(invokeRequest, cancellationToken);
        var invokeResponseBody = await invokeResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!invokeResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Runtime Interface Emulator retornou falha de invocação ({invokeResponse.StatusCode}) — " +
                $"provável erro de inicialização/crash do host Native AOT (ver logs do container): {invokeResponseBody}");
        }

        var lambdaResponse = JsonSerializer.Deserialize(invokeResponseBody, RieJsonContext.Default.ApiGatewayV2Response)
            ?? throw new InvalidOperationException($"Resposta do RIE não pôde ser desserializada: {invokeResponseBody}");

        var responseBody = lambdaResponse.IsBase64Encoded && lambdaResponse.Body is not null
            ? Encoding.UTF8.GetString(Convert.FromBase64String(lambdaResponse.Body))
            : lambdaResponse.Body ?? string.Empty;

        return new TransportResponse(
            lambdaResponse.StatusCode,
            responseBody,
            lambdaResponse.Headers ?? new Dictionary<string, string>());
    }

    public void Dispose() => _rieClient.Dispose();
}

// Subconjunto do payload format 2.0 (API Gateway HTTP API) — só os
// campos que a aplicação de fato lê (ver Amazon.Lambda.AspNetCoreServer,
// APIGatewayHttpApiV2ProxyRequest/Response no pacote oficial). Definido
// aqui em vez de referenciar Amazon.Lambda.APIGatewayEvents pra manter
// este projeto sem dependência de pacotes específicos de runtime Lambda.
public sealed class ApiGatewayV2Request
{
    [JsonPropertyName("version")] public string Version { get; set; } = "2.0";
    [JsonPropertyName("routeKey")] public string RouteKey { get; set; } = "$default";
    [JsonPropertyName("rawPath")] public string RawPath { get; set; } = "";
    [JsonPropertyName("rawQueryString")] public string RawQueryString { get; set; } = "";
    [JsonPropertyName("headers")] public Dictionary<string, string> Headers { get; set; } = new();
    [JsonPropertyName("requestContext")] public ApiGatewayV2RequestContext RequestContext { get; set; } = new();
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("isBase64Encoded")] public bool IsBase64Encoded { get; set; }
}

public sealed class ApiGatewayV2RequestContext
{
    [JsonPropertyName("http")] public ApiGatewayV2Http Http { get; set; } = new();
}

public sealed class ApiGatewayV2Http
{
    [JsonPropertyName("method")] public string Method { get; set; } = "GET";
    [JsonPropertyName("path")] public string Path { get; set; } = "/";
    [JsonPropertyName("sourceIp")] public string SourceIp { get; set; } = "127.0.0.1";
}

public sealed class ApiGatewayV2Response
{
    [JsonPropertyName("statusCode")] public int StatusCode { get; set; }
    [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; set; }
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("isBase64Encoded")] public bool IsBase64Encoded { get; set; }
}

[JsonSerializable(typeof(ApiGatewayV2Request))]
[JsonSerializable(typeof(ApiGatewayV2Response))]
internal partial class RieJsonContext : JsonSerializerContext
{
}
