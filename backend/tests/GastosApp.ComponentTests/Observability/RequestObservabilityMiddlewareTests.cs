using FluentAssertions;
using GastosApp.Api.Common;
using GastosApp.ComponentTests.Support;
using System.Net;

namespace GastosApp.ComponentTests.Observability;

public sealed class RequestObservabilityMiddlewareTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RequestObservabilityMiddlewareTests(ComponentTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Requisicao_ComTraceIdEnviado_EcoaMesmoValorNaResposta()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(ObservabilityHeaderNames.TraceId, "trace-de-teste-123");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues(ObservabilityHeaderNames.TraceId, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("trace-de-teste-123");
    }

    [Fact]
    public async Task Requisicao_SemTraceIdEnviado_RecebeTraceIdGeradoNaResposta()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues(ObservabilityHeaderNames.TraceId, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Requisicao_ComErro_AindaAssimRecebeHeaderTraceId()
    {
        // Rota protegida sem Authorization — TestAuthHandler devolve 401.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.TryGetValues(ObservabilityHeaderNames.TraceId, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Requisicao_ComSessionIdClientPlatformClientVersionAusentes_ContinuaFuncionandoNormalmente()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
