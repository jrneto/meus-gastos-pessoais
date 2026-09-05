using FluentAssertions;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Observability;

/// <summary>
/// Valida o RequestObservabilityMiddleware (FEAT-38) contra o binário
/// Native AOT de verdade (run-local.sh) — risco real de
/// JsonDocument/buffer de stream sob Lambda, não coberto pelos testes
/// unitário/componente (que rodam sob JIT). Não é "endpoint novo": só
/// reaproveita GET /health como qualquer rota já existente serviria.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ObservabilityFlowTests
{
    [Fact]
    public async Task Health_QualquerRequisicao_RecebeHeaderTraceIdNaResposta()
    {
        using var transport = ApiTransportFactory.Create();

        var response = await transport.SendAsync(HttpMethod.Get, "/health");

        response.StatusCode.Should().Be(200);
        response.Headers.Should().ContainKey("trace-id");
        response.Headers["trace-id"].Should().NotBeNullOrWhiteSpace();
    }
}
