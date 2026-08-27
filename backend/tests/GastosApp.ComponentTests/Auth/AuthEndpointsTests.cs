using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Auth;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Users;
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
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _factory.ResetUserProfileRepositoryMock();
        _client = factory.CreateClient();
    }

    private static readonly object ValidRegisterRequest = new
    {
        email = "neto@email.com",
        password = "Senha123",
        name = "Fulano da Silva",
        phoneNumber = "11999998888",
        cpf = "11144477735"
    };

    [Fact]
    public async Task Register_ComDadosValidos_Retorna201ComLocationEBody()
    {
        _factory.AuthServiceMock
            .RegisterAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new RegisterResult("uuid-123", "neto@email.com"))));

        var response = await _client.PostAsJsonAsync("/auth/register", ValidRegisterRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be("/auth/me");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.GetProperty("email").GetString().Should().Be("neto@email.com");
        body.GetProperty("name").GetString().Should().Be("Fulano da Silva");
        body.GetProperty("phoneNumber").GetString().Should().Be("11999998888");
        body.GetProperty("cpf").GetString().Should().Be("11144477735");
    }

    [Fact]
    public async Task Register_ComEmailDuplicado_Retorna409()
    {
        _factory.AuthServiceMock
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists)));

        var response = await _client.PostAsJsonAsync("/auth/register", ValidRegisterRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/email-already-exists");
    }

    [Fact]
    public async Task Register_ComCpfJaCadastrado_Retorna409EDesfazCadastroNoCognito()
    {
        _factory.AuthServiceMock
            .RegisterAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new RegisterResult("uuid-123", "neto@email.com"))));
        _factory.UserProfileRepositoryMock
            .CreateAsync(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserProfileResult(CpfAlreadyExists: true));

        var response = await _client.PostAsJsonAsync("/auth/register", ValidRegisterRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/cpf-already-exists");

        await _factory.AuthServiceMock.Received(1).DeleteAsync("neto@email.com", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Senha123", "Fulano da Silva", "11999998888", "11144477735")]
    [InlineData("neto@email.com", "", "Fulano da Silva", "11999998888", "11144477735")]
    [InlineData("neto@email.com", "123", "Fulano da Silva", "11999998888", "11144477735")]
    [InlineData("neto@email.com", "Senha123", "", "11999998888", "11144477735")]
    [InlineData("neto@email.com", "Senha123", "A", "11999998888", "11144477735")]
    [InlineData("neto@email.com", "Senha123", "Fulano da Silva", "", "11144477735")]
    [InlineData("neto@email.com", "Senha123", "Fulano da Silva", "(11) 99999-8888", "11144477735")]
    [InlineData("neto@email.com", "Senha123", "Fulano da Silva", "11999998888", "")]
    [InlineData("neto@email.com", "Senha123", "Fulano da Silva", "11999998888", "11111111111")]
    [InlineData("neto@email.com", "Senha123", "Fulano da Silva", "11999998888", "11144477736")]
    public async Task Register_ComParametrosInvalidos_Retorna400SemChamarAuthService(
        string email, string password, string name, string phoneNumber, string cpf)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { email, password, name, phoneNumber, cpf });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        // "validation-error", não mais "bad-request": a validação de RegisterUserCommand
        // migrou pro ValidationBehavior (mesmo pipeline usado por Category/Transaction/etc.),
        // que sempre usa esse código (ver ValidationBehavior.cs) — descoberto durante os
        // testes desta feature, spec.md corrigido de acordo (não há frontend consumindo
        // o "type" literal ainda).
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        _ = _factory.AuthServiceMock.DidNotReceive()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_Retorna200()
    {
        _factory.AuthServiceMock
            .LoginAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-123", "refresh-token-abc"))));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().Be("eyJ...");
        body.GetProperty("expiresIn").GetInt32().Should().Be(3600);
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.Single();
        cookie.Should().StartWith("refreshToken=refresh-token-abc");
        var cookieLower = cookie.ToLowerInvariant();
        cookieLower.Should().Contain("httponly");
        cookieLower.Should().Contain("secure");
        cookieLower.Should().Contain("samesite=strict");
        cookieLower.Should().Contain("path=/auth");
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
    public async Task Login_ComUsuarioNaoConfirmado_Retorna401ComUserNotConfirmed()
    {
        _factory.AuthServiceMock
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<LoginResult>(AuthErrors.UserNotConfirmed)));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/user-not-confirmed");
    }

    // FEAT-19: login é o fallback de criação de conta — cobre o caso do
    // trigger PostConfirmation do Cognito não ter rodado ainda (ou nunca
    // rodar, ex.: ambiente local).
    [Fact]
    public async Task Login_ComUsuarioSemContaAinda_CriaAccountViaFallback()
    {
        _factory.AuthServiceMock
            .LoginAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-sem-conta", "refresh-token-abc"))));
        _factory.AccountRepositoryMock
            .FindAccountIdByUserIdAsync("uuid-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _factory.AccountRepositoryMock
            .CreateAsync("uuid-sem-conta", "neto@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        // Contrato de login não muda — resposta idêntica de sempre.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-sem-conta");

        await _factory.AccountRepositoryMock.Received(1)
            .CreateAsync("uuid-sem-conta", "neto@email.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_ComUsuarioComContaExistente_NaoCriaDuplicata()
    {
        _factory.AuthServiceMock
            .LoginAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-123", "refresh-token-abc"))));
        _factory.AccountRepositoryMock
            .FindAccountIdByUserIdAsync("uuid-123", Arg.Any<CancellationToken>())
            .Returns("account-ja-existente");

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.AccountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact]
    public async Task Login_ComCredenciaisInvalidas_NaoCriaAccount()
    {
        _factory.AuthServiceMock
            .LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<LoginResult>(AuthErrors.InvalidCredentials)));

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.AccountRepositoryMock.DidNotReceiveWithAnyArgs()
            .FindAccountIdByUserIdAsync(default!, default);
        await _factory.AccountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    // FEAT-20: login é também o momento em que convites pendentes pro e-mail
    // do usuário são aceitos, trocando a conta ativa dele.
    [Fact]
    public async Task Login_ComConvitePendenteParaOEmail_AceitaETrocaContaAtiva()
    {
        _factory.AuthServiceMock
            .LoginAsync("convidado@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-convidado", "refresh-token-abc"))));
        _factory.AccountRepositoryMock
            .FindAccountIdByUserIdAsync("uuid-convidado", Arg.Any<CancellationToken>())
            .Returns("account-propria");
        _factory.MembershipRepositoryMock
            .AcceptPendingInvitesByEmailAsync("convidado@email.com", "uuid-convidado", Arg.Any<CancellationToken>())
            .Returns(new List<AcceptedInvite> { new("account-convite", DateTimeOffset.UtcNow) });

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "convidado@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.AccountRepositoryMock.Received(1)
            .SetActiveAccountAsync("uuid-convidado", "account-convite", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_SemConvitePendente_NaoTrocaContaAtiva()
    {
        _factory.AuthServiceMock
            .LoginAsync("neto@email.com", "Senha123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new LoginResult("eyJ...", 3600, "uuid-123", "refresh-token-abc"))));
        _factory.AccountRepositoryMock
            .FindAccountIdByUserIdAsync("uuid-123", Arg.Any<CancellationToken>())
            .Returns("account-ja-existente");
        _factory.MembershipRepositoryMock
            .AcceptPendingInvitesByEmailAsync("neto@email.com", "uuid-123", Arg.Any<CancellationToken>())
            .Returns(new List<AcceptedInvite>());

        var response = await _client.PostAsJsonAsync("/auth/login", new { email = "neto@email.com", password = "Senha123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.AccountRepositoryMock.DidNotReceiveWithAnyArgs()
            .SetActiveAccountAsync(default!, default!, default);
    }

    [Fact]
    public async Task Refresh_ComCookieValido_Retorna200()
    {
        _factory.AuthServiceMock
            .RefreshAsync("refresh-token-abc", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new RefreshResult("novo-access-token", 3600, "uuid-123"))));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", "refreshToken=refresh-token-abc");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().Be("novo-access-token");
        body.GetProperty("expiresIn").GetInt32().Should().Be(3600);
        body.GetProperty("userId").GetString().Should().Be("uuid-123");

        response.Headers.TryGetValues("Set-Cookie", out _).Should().BeFalse("sem rotação, o cookie não é reescrito no sucesso");
    }

    [Fact]
    public async Task Refresh_SemCookie_Retorna401()
    {
        var response = await _client.PostAsync("/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/refresh-token-missing");

        await _factory.AuthServiceMock.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default);
    }

    [Fact]
    public async Task Refresh_ComCookieInvalidoOuExpirado_Retorna401ELimpaCookie()
    {
        _factory.AuthServiceMock
            .RefreshAsync("refresh-token-expirado", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken)));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", "refreshToken=refresh-token-expirado");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/invalid-refresh-token");

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.Single();
        cookie.Should().Contain("refreshToken=");
        cookie.Should().Contain("expires=Thu, 01 Jan 1970");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logout_ComOuSemCookie_Retorna200ELimpaCookie(bool comCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        if (comCookie)
            request.Headers.Add("Cookie", "refreshToken=refresh-token-abc");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.Single();
        cookie.Should().Contain("refreshToken=");
        cookie.Should().Contain("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public async Task Me_ComPerfilCadastrado_Retorna200ComNomeTelefoneCpf()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "uuid-123|neto@email.com|Neto");
        _factory.UserProfileRepositoryMock
            .FindByUserIdAsync("uuid-123", Arg.Any<CancellationToken>())
            .Returns(UserProfile.Restore("uuid-123", "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.GetProperty("email").GetString().Should().Be("neto@email.com");
        body.GetProperty("name").GetString().Should().Be("Fulano da Silva");
        body.GetProperty("phoneNumber").GetString().Should().Be("11999998888");
        body.GetProperty("cpf").GetString().Should().Be("11144477735");
    }

    [Fact]
    public async Task Me_SemPerfilCadastrado_Retorna200ComCamposNulos()
    {
        // Usuário cadastrado antes desta feature (sem migração de dados, backlog.md) —
        // FindByUserIdAsync sem configuração já retorna null (default do mock).
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "uuid-123|neto@email.com|Neto");

        var response = await _client.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("userId").GetString().Should().Be("uuid-123");
        body.GetProperty("email").GetString().Should().Be("neto@email.com");
        body.GetProperty("name").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("phoneNumber").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("cpf").ValueKind.Should().Be(JsonValueKind.Null);
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

        var response = await _client.PostAsJsonAsync("/auth/register", ValidRegisterRequest);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }
}