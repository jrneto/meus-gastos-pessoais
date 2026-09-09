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

    [Fact]
    public async Task Preflight_RequisicaoOptions_NaoPassaPelaObservabilidadeENaoRecebeTraceId()
    {
        // Preflight de CORS é sempre 204 sem valor de negócio — o
        // middleware dá early-return antes de setar o header trace-id
        // ou gerar o log "Requisição concluída" (evita custo de
        // armazenamento à toa no CloudWatch com uma linha por preflight).
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.Contains(ObservabilityHeaderNames.TraceId).Should().BeFalse();
    }
}
