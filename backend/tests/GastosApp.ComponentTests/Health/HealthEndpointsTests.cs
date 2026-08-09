using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GastosApp.Application.Health;
using GastosApp.ComponentTests.Support;

namespace GastosApp.ComponentTests.Health;

public sealed class HealthEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;

    public HealthEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_SemAutenticacao_Retorna200ComFallbackLocal()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("ok");
        body.Version.Should().Be("local");
        body.CommitSha.Should().Be("unknown");
        body.Environment.Should().Be("local");
    }

    [Fact]
    public async Task GetHealth_ComVariaveisDeVersaoConfiguradas_RetornaValoresConfigurados()
    {
        var factoryWithVersion = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("APP_VERSION", "v1.4.0");
            builder.UseSetting("APP_COMMIT_SHA", "abc1234");
            builder.UseSetting("APP_ENVIRONMENT", "prod");
        });
        var client = factoryWithVersion.CreateClient();

        var response = await client.GetAsync("/health");

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Version.Should().Be("v1.4.0");
        body.CommitSha.Should().Be("abc1234");
        body.Environment.Should().Be("prod");
    }
}
