using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Transporte usado contra Homologação, Produção e o modo local "via
/// Kestrel" — API real chamada como qualquer cliente HTTP chamaria.
/// </summary>
public sealed class DirectHttpTransport : IApiTransport, IDisposable
{
    private const int MaxRedirects = 5;

    private readonly HttpClient _client;

    public DirectHttpTransport(string baseUrl)
    {
        // AllowAutoRedirect=false + redirect manual em SendAsync (ver
        // abaixo) — achado real: em local (Kestrel), Program.cs chama
        // `app.UseHttpsRedirection()` incondicionalmente, e quando a Api
        // sobe nas duas portas (http E https — perfil "https" de
        // launchSettings.json, o que o VS Code usa por padrão pra rodar
        // a config "GastosApp.Api"), toda requisição http vira um 307
        // pra https. O `AllowAutoRedirect` default do HttpClient segue
        // esse 307 automaticamente, mas o .NET **remove o header
        // Authorization** ao seguir um redirect que muda scheme/porta —
        // GET /auth/me (autenticado) responde 401, enquanto
        // /auth/register e /auth/login (sem Authorization) passam
        // batido, exatamente o padrão observado. Seguir o redirect
        // manualmente aqui, preservando todos os headers, corrige isso
        // sem precisar mudar Program.cs (comportamento de produção) nem
        // depender de qual porta o VS Code escolhe usar.
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        _client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<TransportResponse> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearerToken = null,
        CancellationToken cancellationToken = default)
    {
        string? json = body is null ? null : JsonSerializer.Serialize(body, JsonDefaults.Options);

        var currentUri = new Uri(_client.BaseAddress!, path);
        for (var redirectCount = 0; ; redirectCount++)
        {
            using var request = new HttpRequestMessage(method, currentUri);

            if (json is not null)
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await _client.SendAsync(request, cancellationToken);

            var isRedirect = response.StatusCode is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Found
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;

            if (isRedirect && response.Headers.Location is not null && redirectCount < MaxRedirects)
            {
                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
                continue;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var headers = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            return new TransportResponse((int)response.StatusCode, responseBody, headers);
        }
    }

    public void Dispose() => _client.Dispose();
}
