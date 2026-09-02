using Amazon.CognitoIdentityProvider.Model;
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

    // FEAT-35: cobertura do fluxo de sucesso real de POST /auth/confirm sem
    // precisar do código de 6 dígitos de fato (a suíte não tem acesso ao
    // e-mail — ver plan.md, decisão técnica 5). TestAccountFixture já
    // confirma a conta via AdminConfirmSignUp, então chamar /auth/confirm
    // de novo aqui exercita o branch de idempotência (NotAuthorizedException
    // → 200) contra o Cognito real.
    //
    // Pulado em modo Local: o código-fonte do cognito-local v5.3.0
    // (lib/targets/confirmSignUp.js) nunca checa UserStatus — só compara o
    // ConfirmationCode salvo, então o branch "usuário já confirmado" do
    // Cognito real (que dispara ANTES de checar o código) é inalcançável
    // contra o emulador; AdminConfirmSignUp também não limpa o
    // ConfirmationCode salvo. Verificado empiricamente rodando run-local.sh
    // (achado registrado em backend/docs/backlog.md) — validado de verdade
    // só contra Cognito real (hom/prod), via backend-integration-tests-hom.yml.
    [Fact]
    public async Task Confirm_UsuarioJaConfirmado_Retorna200Idempotente()
    {
        if (IntegrationTestEnvironment.Current.IsLocal)
        {
            Console.WriteLine("SKIP (modo Local): cognito-local não reproduz a checagem de UserStatus do ConfirmSignUp real — ver comentário no teste.");
            return;
        }

        await using var account = await TestAccountFixture.CreateAsync();

        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/confirm",
            new ConfirmRequestDto(account.Email, "000000"));

        response.StatusCode.Should().Be(200);
    }

    // Diverge da redação literal da task 29 (tasks.md), que descrevia
    // reusar a mesma fixture já confirmada — mas spec.md US2 já definia
    // "código incorreto" como cenário de usuário NÃO confirmado, e o
    // catch de NotAuthorizedException (idempotência) independe do código
    // enviado ("qualquer code", spec.md US5) — reusar uma conta já
    // confirmada aqui sempre daria 200, nunca 400. Registra uma conta
    // deliberadamente não confirmada (mesmo padrão de
    // ResendConfirmation_UsuarioNaoConfirmado_Retorna200) e limpa no
    // Cognito manualmente.
    [Fact]
    public async Task Confirm_CodigoIncorreto_Retorna400()
    {
        var env = IntegrationTestEnvironment.Current;
        using var transport = ApiTransportFactory.Create(env);
        using var cognito = AwsClientFactory.CreateCognitoClient(env);

        var email = $"int-test+{Guid.NewGuid():N}@jrnexpenses.com";

        var registerResponse = await transport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(email, "Teste@Integrado123", "Conta Não Confirmada", "11999999999", CpfGenerator.GenerateUnique()));

        registerResponse.StatusCode.Should().Be(201);

        try
        {
            var response = await transport.SendAsync(
                HttpMethod.Post, "/auth/confirm",
                new ConfirmRequestDto(email, "000000"));

            response.StatusCode.Should().Be(400);

            var problem = response.Deserialize<ProblemDetailsDto>();
            problem.Type.Should().Be("https://gastosapp.dev/errors/invalid-confirmation-code");
        }
        finally
        {
            var userPoolId = await TestAccountFixture.ResolveUserPoolIdAsync(env);
            await cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
            {
                UserPoolId = userPoolId,
                Username = email
            });
        }
    }

    // Pulado em modo Local: o cognito-local v5.3.0 lança NotAuthorizedError
    // (não UserNotFoundError, como o Cognito real, ver AWS SDK docs) quando
    // getUserByUsername não encontra o usuário em ConfirmSignUp — nosso
    // catch de NotAuthorizedException (idempotência, "já confirmado") acaba
    // absorvendo isso como sucesso (200) em vez do esperado 400. Verificado
    // empiricamente rodando run-local.sh; validado contra Cognito real
    // (hom/prod) via backend-integration-tests-hom.yml.
    [Fact]
    public async Task Confirm_EmailInexistente_Retorna400()
    {
        if (IntegrationTestEnvironment.Current.IsLocal)
        {
            Console.WriteLine("SKIP (modo Local): cognito-local lança NotAuthorizedException (não UserNotFoundException) pra usuário inexistente em ConfirmSignUp — ver comentário no teste.");
            return;
        }

        using var transport = ApiTransportFactory.Create();

        var response = await transport.SendAsync(
            HttpMethod.Post, "/auth/confirm",
            new ConfirmRequestDto($"inexistente+{Guid.NewGuid():N}@jrnexpenses.com", "000000"));

        response.StatusCode.Should().Be(400);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/invalid-confirmation-code");
    }

    // Pulado em modo Local: o cognito-local v5.3.0 não implementa
    // ResendConfirmationCode (nenhum lib/targets/resendConfirmationCode.js
    // existe no pacote — confirmado inspecionando o container; última
    // versão publicada, sem correção pendente aceita upstream) — a chamada
    // ao SDK bate numa operação inexistente no emulador e propaga como 500.
    // Validado contra Cognito real (hom/prod) via backend-integration-tests-hom.yml.
    [Fact]
    public async Task ResendConfirmation_UsuarioNaoConfirmado_Retorna200()
    {
        var env = IntegrationTestEnvironment.Current;

        if (env.IsLocal)
        {
            Console.WriteLine("SKIP (modo Local): cognito-local não implementa ResendConfirmationCode — ver comentário no teste.");
            return;
        }

        using var transport = ApiTransportFactory.Create(env);
        using var cognito = AwsClientFactory.CreateCognitoClient(env);

        var email = $"int-test+{Guid.NewGuid():N}@jrnexpenses.com";

        var registerResponse = await transport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(email, "Teste@Integrado123", "Conta Não Confirmada", "11999999999", CpfGenerator.GenerateUnique()));

        registerResponse.StatusCode.Should().Be(201);

        try
        {
            var response = await transport.SendAsync(
                HttpMethod.Post, "/auth/resend-confirmation",
                new ResendConfirmationRequestDto(email));

            response.StatusCode.Should().Be(200);
        }
        finally
        {
            var userPoolId = await TestAccountFixture.ResolveUserPoolIdAsync(env);
            await cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
            {
                UserPoolId = userPoolId,
                Username = email
            });
        }
    }
}

file sealed record MeResponseDto(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf);
