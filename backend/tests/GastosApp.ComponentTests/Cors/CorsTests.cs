using FluentAssertions;
using GastosApp.ComponentTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace GastosApp.ComponentTests.Cors;

public sealed class CorsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private const string DevOrigin = "http://localhost:5173";
    private const string ProductionOrigin = "https://jrnexpenses.com";
    private const string DisallowedOrigin = "https://not-allowed.example.com";

    private readonly ComponentTestWebApplicationFactory _factory;

    public CorsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithCorsConfig()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Cors:AllowedOrigins:0", DevOrigin);
            builder.UseSetting("Cors:ProductionOrigins:0", ProductionOrigin);
        });

        return factory.CreateClient();
    }

    [Fact]
    public async Task Requisicao_ComOrigemDeAllowedOrigins_RecebeAccessControlAllowOriginCorrespondente()
    {
        var client = CreateClientWithCorsConfig();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/expenses");
        request.Headers.Add("Origin", DevOrigin);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(DevOrigin);
    }

    [Fact]
    public async Task Requisicao_ComOrigemDeProductionOrigins_RecebeAccessControlAllowOriginCorrespondente()
    {
        var client = CreateClientWithCorsConfig();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/expenses");
        request.Headers.Add("Origin", ProductionOrigin);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(ProductionOrigin);
    }

    [Fact]
    public async Task Requisicao_ComOrigemForaDasListasPermitidas_NaoRecebeAccessControlAllowOrigin()
    {
        var client = CreateClientWithCorsConfig();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/expenses");
        request.Headers.Add("Origin", DisallowedOrigin);

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Preflight_ComOrigemDeProductionOrigins_RecebeAccessControlAllowOriginCorrespondente()
    {
        var client = CreateClientWithCorsConfig();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/expenses");
        request.Headers.Add("Origin", ProductionOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be(ProductionOrigin);
    }

    [Fact]
    public async Task Requisicao_ComOrigemPermitida_RecebeAccessControlAllowCredentialsTrue()
    {
        // Necessário para o cookie httpOnly de refresh token (FEAT-15)
        // ser enviado pelo navegador em chamadas com `credentials:
        // 'include'` (consumo no frontend, FEAT-12).
        var client = CreateClientWithCorsConfig();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/expenses");
        request.Headers.Add("Origin", DevOrigin);

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("true");
    }
}
