using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Transporte usado contra Homologação e Produção — API real por trás de
/// API Gateway/Lambda, chamada como qualquer cliente HTTP chamaria.
/// </summary>
public sealed class DirectHttpTransport : IApiTransport, IDisposable
{
    private readonly HttpClient _client;

    public DirectHttpTransport(string baseUrl)
    {
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<TransportResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonDefaults.Options);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        return new TransportResponse((int)response.StatusCode, responseBody, headers);
    }

    public void Dispose() => _client.Dispose();
}
