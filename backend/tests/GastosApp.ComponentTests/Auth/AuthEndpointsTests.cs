using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Auth;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.ComponentTests.Support;
using NSubstitute;

namespace GastosApp.ComponentTests.Auth;

public sealed class AuthEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAuthServiceMock();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ComDadosValidos_Retorna201ComLocationEBody()
    {
        _factory.AuthServiceMock
            .RegisterAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new RegisterResult("uuid-123", "neto@email.com"))));

        var response = await _client.PostAsJsonAsync("/auth/register", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/auth/me");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.GetProperty("email").GetString().Should().Be("neto@email.com");
    }

    [Fact]
    public async Task Register_ComEmailDuplicado_Retorna409()
    {
        _factory.AuthServiceMock
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists)));

        var response = await _client.PostAsJsonAsync("/auth/register", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/email-already-exists");
    }

    [Theory]
    [InlineData("", "Senha123")]
    [InlineData("neto@email.com", "")]
    [InlineData("neto@email.com", "123")]
    public async Task Register_ComParametrosInvalidos_Retorna400SemChamarAuthService(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { email, password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/bad-request");

        _ = _factory.AuthServiceMock.DidNotReceive()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_Retorna200()
    {
        _factory.AuthServiceMock
            .LoginAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-123"))));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().Be("eyJ...");
        body.GetProperty("expiresIn").GetInt32().Should().Be(3600);
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
    }

    [Fact]
    public async Task Login_ComCredenciaisInvalidas_Retorna401()
    {
        _factory.AuthServiceMock
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<LoginResult>(AuthErrors.InvalidCredentials)));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/invalid-credentials");
    }

    [Fact]
    public async Task Me_ComHeaderDeAutenticacaoDeTeste_Retorna200ComDadosDoUsuario()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "uuid-123|neto@email.com|Neto");

        var response = await _client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.GetProperty("email").GetString().Should().Be("neto@email.com");
        body.GetProperty("name").GetString().Should().Be("Neto");
    }

    [Fact]
    public async Task Me_SemHeaderDeAutenticacao_Retorna401()
    {
        var response = await _client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");
    }

    [Fact]
    public async Task Register_QuandoAuthServiceLancaExcecaoNaoPrevista_Retorna500()
    {
        _factory.AuthServiceMock
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Result<RegisterResult>>(new InvalidOperationException("Falha simulada")));

        var response = await _client.PostAsJsonAsync("/auth/register", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }
}