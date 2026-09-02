using FluentAssertions;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Auth;

/// <summary>
/// Primeiro módulo coberto por teste integrado (FEAT-29) — register,
/// confirmação (via AdminConfirmSignUp) e login contra a API real
/// (Cognito + DynamoDB reais em Hom/Prod; LocalStack + cognito-local via
/// o container Native AOT em Local). Também cobre o módulo Perfil
/// (FEAT-26 — name/phoneNumber/cpf em GET /auth/me, unicidade de CPF),
/// que não tem endpoint próprio (FEAT-32). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuthFlowTests
{
    [Fact]
    public async Task RegisterConfirmLogin_FluxoCompleto_RetornaAccessTokenValido()
    {
        // O próprio setup do fixture já exercita register → confirm →
        // login via HTTP real contra a API — chegar aqui sem lançar já
        // valida boa parte do fluxo.
        await using var account = await TestAccountFixture.CreateAsync();

        account.UserId.Should().NotBeNullOrWhiteSpace();
        account.AccessToken.Should().NotBeNullOrWhiteSpace();

        // GET /auth/me com o token recém-obtido confirma que o token é
        // aceito pelo middleware de autenticação real (JWT validado
        // contra o JWKS do Cognito/cognito-local, não um dublê).
        var meResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/auth/me", bearerToken: account.AccessToken);

        meResponse.StatusCode.Should().Be(200);

        var me = meResponse.Deserialize<MeResponseDto>();
        me.UserId.Should().Be(account.UserId);
        me.Email.Should().Be(account.Email);

        // Perfil (FEAT-26): name/phoneNumber/cpf gravados no registro
        // (TestAccountFixture.SetupAsync) precisam voltar idênticos.
        me.Name.Should().Be("Conta de Teste Integrado");
        me.PhoneNumber.Should().Be("11999999999");
        me.Cpf.Should().Be(account.Cpf);
    }

    [Fact]
    public async Task Register_CpfJaCadastrado_Retorna409()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        using var transport = ApiTransportFactory.Create();
        var response = await transport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(
                $"int-test+{Guid.NewGuid():N}@jrnexpenses.com", "OutraSenha@123",
                "Outro Nome", "11888888888", account.Cpf));

        response.StatusCode.Should().Be(409);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/cpf-already-exists");
    }

    [Fact]
    public async Task Register_EmailJaCadastrado_Retorna409()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        // Mesmo e-mail já confirmado no setup — segunda tentativa de
        // registro precisa colidir no Cognito real (UsernameExistsException).
        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(account.Email, "OutraSenha@123", "Outro Nome", "11888888888", CpfGenerator.GenerateUnique()));

        response.StatusCode.Should().Be(409);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/email-already-exists");
    }

    [Fact]
    public async Task Login_CredenciaisInvalidas_Retorna401()
    {
        using var transport = ApiTransportFactory.Create();

        // Sem depender de nenhuma conta pré-existente: usuário inexistente
        // e usuário com senha errada retornam o mesmo erro mapeado
        // (AuthErrors.InvalidCredentials — CognitoAuthService trata
        // UserNotFoundException e NotAuthorizedException da mesma forma).
        var response = await transport.SendAsync(
            HttpMethod.Post, "/auth/login",
            new LoginRequestDto($"inexistente+{Guid.NewGuid():N}@jrnexpenses.com", "SenhaQualquer@123"));

        response.StatusCode.Should().Be(401);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/invalid-credentials");
    }
}

file sealed record MeResponseDto(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf);
