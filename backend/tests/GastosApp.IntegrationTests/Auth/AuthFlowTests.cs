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

    // FEAT-35, corrigido após a primeira execução real contra hom
    // (backend-integration-tests-hom.yml, 2026-09-02): a suposição original
    // deste teste — que ConfirmSignUp contra usuário já confirmado, com
    // QUALQUER código, cai em NotAuthorizedException e é tratado como
    // idempotente (200) — não se confirmou. Verificado empiricamente contra
    // Cognito real (não só cognito-local): com um código arbitrário
    // ("000000"), o Cognito real lança CodeMismatchException (400
    // invalid-confirmation-code), igual a qualquer outro código incorreto —
    // não há tratamento especial por "já confirmado" nesse caminho. A
    // idempotência real (resubmeter o MESMO código que já confirmou a
    // conta) continua sem cobertura automatizada — exigiria capturar o
    // código genuíno enviado por e-mail, que esta suíte não tem acesso (ver
    // plan.md, decisão técnica 5). O catch de NotAuthorizedException
    // permanece em CognitoAuthService (defensivo, categoria documentada na
    // API do Cognito), só não é mais o que este teste exercita.
    [Fact]
    public async Task Confirm_UsuarioJaConfirmado_ComCodigoIncorreto_Retorna400()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/confirm",
            new ConfirmRequestDto(account.Email, "000000"));

        response.StatusCode.Should().Be(400);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/invalid-confirmation-code");
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
    // empiricamente rodando run-local.sh.
    //
    // Type corrigido após a primeira execução real contra hom
    // (backend-integration-tests-hom.yml, 2026-09-02): esperava-se
    // invalid-confirmation-code (UserNotFoundException), mas o
    // prevent_user_existence_errors="ENABLED" do User Pool (cognito.tf,
    // hom/prod) faz o Cognito real lançar ExpiredCodeException pra usuário
    // inexistente — resposta genérica de anti-enumeração, documentada pela
    // AWS. UserNotFoundException nunca chega a esse ponto de fato contra o
    // serviço real; o catch continua em CognitoAuthService por segurança
    // (não custa manter), mas ExpiredCodeException é o caminho real.
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
        problem.Type.Should().Be("https://gastosapp.dev/errors/expired-confirmation-code");
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

    // FEAT-36: cobertura do fluxo de sucesso real de POST /auth/forgot-password
    // sem precisar do código de 6 dígitos de fato (a suíte não tem acesso ao
    // e-mail — mesma limitação da FEAT-35, ver plan.md).
    [Fact]
    public async Task ForgotPassword_EmailDeContaExistente_Retorna200()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/forgot-password",
            new ForgotPasswordRequestDto(account.Email));

        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ForgotPassword_EmailInexistente_Retorna200()
    {
        using var transport = ApiTransportFactory.Create();

        var response = await transport.SendAsync(
            HttpMethod.Post, "/auth/forgot-password",
            new ForgotPasswordRequestDto($"inexistente+{Guid.NewGuid():N}@jrnexpenses.com"));

        response.StatusCode.Should().Be(200);
    }

    // Corrigido após a primeira execução real contra hom
    // (backend-integration-tests-hom.yml, 2026-09-02): a versão original
    // deste teste nunca chamava POST /auth/forgot-password antes do reset —
    // sem uma sessão de código pendente de verdade, o Cognito real não
    // consegue distinguir "código errado" de "nenhum código ativo" e lança
    // ExpiredCodeException (mesmo caminho do "email inexistente" abaixo), não
    // CodeMismatchException. Contra cognito-local (mais simplificado) isso
    // "funcionava" por acidente. Corrigido gerando um código pendente de
    // verdade primeiro — só assim o Cognito tem algo genuíno pra comparar e
    // lança CodeMismatchException (400 invalid-reset-code) de fato.
    [Fact]
    public async Task ResetPassword_CodigoIncorreto_Retorna400()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/forgot-password",
            new ForgotPasswordRequestDto(account.Email));

        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/auth/reset-password",
            new ResetPasswordRequestDto(account.Email, "000000", "OutraSenha@2026"));

        response.StatusCode.Should().Be(400);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/invalid-reset-code");
    }

    // Pulado em modo Local: type corrigido após a primeira execução real
    // contra hom (backend-integration-tests-hom.yml, 2026-09-02) — esperava-se
    // invalid-reset-code (UserNotFoundException), mas o
    // prevent_user_existence_errors="ENABLED" do User Pool (cognito.tf,
    // hom/prod) faz o Cognito real lançar ExpiredCodeException pra usuário
    // inexistente em ConfirmForgotPassword também (mesma resposta genérica de
    // anti-enumeração documentada pela AWS que afeta ConfirmSignUp — ver
    // Confirm_EmailInexistente_Retorna400). cognito-local não reproduz isso
    // (lança UserNotFoundException de fato), então local e hom divergem aqui.
    [Fact]
    public async Task ResetPassword_EmailInexistente_Retorna400()
    {
        if (IntegrationTestEnvironment.Current.IsLocal)
        {
            Console.WriteLine("SKIP (modo Local): cognito-local lança UserNotFoundException (não ExpiredCodeException) pra usuário inexistente em ConfirmForgotPassword — ver comentário no teste.");
            return;
        }

        using var transport = ApiTransportFactory.Create();

        var response = await transport.SendAsync(
            HttpMethod.Post, "/auth/reset-password",
            new ResetPasswordRequestDto($"inexistente+{Guid.NewGuid():N}@jrnexpenses.com", "000000", "OutraSenha@2026"));

        response.StatusCode.Should().Be(400);

        var problem = response.Deserialize<ProblemDetailsDto>();
        problem.Type.Should().Be("https://gastosapp.dev/errors/expired-reset-code");
    }

    // Não existe um teste "ResetPassword_SenhaForaDaPolitica_Retorna400" nesta
    // suíte — investigado durante a implementação da FEAT-36 (plan.md, ponto
    // de confirmação 2): confirmado empiricamente (curl direto contra a Api
    // local + cognito-local) que ConfirmForgotPassword valida o CÓDIGO antes
    // da SENHA — código errado + senha fraca simultaneamente ainda retorna
    // 400 invalid-reset-code, nunca bad-request. Como esta suíte não tem
    // acesso ao código real de recuperação (só chega por e-mail), não há como
    // forçar o caminho "código correto + senha fora da política" para exercitar
    // esse branch especificamente. Coberto pelos testes unitário
    // (ConfirmForgotPasswordAsync_ShouldReturnValidationError_WhenCognitoThrowsInvalidPasswordException,
    // CognitoAuthServiceTests) e de componente
    // (ResetPassword_QuandoAuthServiceRetornaErro_PropagaProblemDetails,
    // AuthEndpointsTests) — validação real do Cognito não exercitada aqui.
}

file sealed record MeResponseDto(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf);
