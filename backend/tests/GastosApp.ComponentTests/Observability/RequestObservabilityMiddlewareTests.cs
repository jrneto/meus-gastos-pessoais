using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Api.Common;
using GastosApp.ComponentTests.Support;

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
        // Os 3 headers obrigatórios (FEAT-39) precisam estar presentes
        // aqui, senão a requisição nem chegaria ao TestAuthHandler (seria
        // rejeitada antes, com 400 — ver testes dedicados abaixo).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        AddRequiredHeaders(request);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.TryGetValues(ObservabilityHeaderNames.TraceId, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Requisicao_SemSessionId_ContinuaFuncionandoNormalmente()
    {
        // session-id continua totalmente opcional (FEAT-39, "Fora do
        // escopo") — numa rota não isenta, com os 3 headers obrigatórios
        // presentes, a ausência de session-id não é motivo de 400: a
        // requisição segue até o TestAuthHandler (401 por falta de
        // Authorization, não por header de observabilidade ausente).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        AddRequiredHeaders(request);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_SemNenhumHeaderDeObservabilidade_Continua200()
    {
        // GET /health fica isento da obrigatoriedade dos 3 headers —
        // continua consultável via curl manual simples (FEAT-39).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Requisicao_SemTraceId_Retorna400ComDetailIdentificandoTraceId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        request.Headers.Add(ObservabilityHeaderNames.ClientPlatform, "web");
        request.Headers.Add(ObservabilityHeaderNames.ClientVersion, "1.0.0");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/missing-observability-headers");
        problem.GetProperty("detail").GetString().Should().Contain(ObservabilityHeaderNames.TraceId);
    }

    [Fact]
    public async Task Requisicao_SemClientPlatform_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        request.Headers.Add(ObservabilityHeaderNames.TraceId, "trace-de-teste");
        request.Headers.Add(ObservabilityHeaderNames.ClientVersion, "1.0.0");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain(ObservabilityHeaderNames.ClientPlatform);
    }

    [Fact]
    public async Task Requisicao_SemClientVersion_Retorna400()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        request.Headers.Add(ObservabilityHeaderNames.TraceId, "trace-de-teste");
        request.Headers.Add(ObservabilityHeaderNames.ClientPlatform, "web");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain(ObservabilityHeaderNames.ClientVersion);
    }

    [Fact]
    public async Task Requisicao_SemNenhumDosTresObrigatorios_Retorna400ComOsTresNoDetail()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = problem.GetProperty("detail").GetString();
        detail.Should().Contain(ObservabilityHeaderNames.TraceId);
        detail.Should().Contain(ObservabilityHeaderNames.ClientPlatform);
        detail.Should().Contain(ObservabilityHeaderNames.ClientVersion);
    }

    [Fact]
    public async Task Requisicao_Rejeitada_AindaAssimRecebeHeaderTraceIdNaResposta()
    {
        // Nenhum dos 3 headers enviado — inclusive trace-id está entre
        // os ausentes, e mesmo assim a resposta de erro carrega um
        // trace-id (gerado pela API só para este response), preservando
        // o hábito de toda resposta ser correlacionável no log.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Headers.TryGetValues(ObservabilityHeaderNames.TraceId, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
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

    private static void AddRequiredHeaders(HttpRequestMessage request)
    {
        request.Headers.Add(ObservabilityHeaderNames.TraceId, "trace-de-teste");
        request.Headers.Add(ObservabilityHeaderNames.ClientPlatform, "web");
        request.Headers.Add(ObservabilityHeaderNames.ClientVersion, "1.0.0");
    }
}
