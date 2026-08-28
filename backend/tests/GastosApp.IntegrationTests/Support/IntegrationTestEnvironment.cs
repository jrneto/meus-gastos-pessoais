namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Alvo contra o qual a suíte roda nesta execução — lido uma única vez
/// de variáveis de ambiente (ver backend/specs/FEAT-29-testes-integrados/plan.md).
/// </summary>
public enum IntegrationTestMode
{
    Local,
    Hom,
    Prod
}

/// <summary>
/// Resolve, a partir de <c>INTEGRATION_TESTS_MODE</c> (e variáveis
/// correlatas), tudo que os testes precisam saber sobre o ambiente-alvo:
/// URL base (hom/prod), como falar com o container local (Runtime
/// Interface Emulator), path do Parameter Store e nome da tabela
/// DynamoDB de cada ambiente (ver backend/infra/CLAUDE.md, "Ambientes").
/// </summary>
public sealed class IntegrationTestEnvironment
{
    public static readonly IntegrationTestEnvironment Current = Load();

    public required IntegrationTestMode Mode { get; init; }

    /// <summary>
    /// URL base da API — usada em Hom/Prod (sempre) e, por padrão, em
    /// Local também (Api via `dotnet run`/Kestrel, JIT normal — permite
    /// breakpoint dentro do código da própria Api, não só do teste).
    /// <see cref="ApiTransportFactory"/> usa <see cref="DirectHttpTransport"/>
    /// sempre que <c>BaseUrl</c> não for nulo. Só fica nulo em Local
    /// quando <c>INTEGRATION_TESTS_TRANSPORT=rie</c> é setada
    /// explicitamente (aí <see cref="ApiTransportFactory"/> usa
    /// <see cref="LambdaRieTransport"/> contra o container Native AOT).
    /// Cognito/DynamoDB continuam via LocalStack/cognito-local nos dois
    /// casos — isso não depende do transporte escolhido aqui. Ver
    /// backend/tests/GastosApp.IntegrationTests/README.md.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>Endpoint do Runtime Interface Emulator — só usado em Local (LambdaRieTransport).</summary>
    public string RieInvokeUrl { get; init; } = "http://localhost:9000/2015-03-31/functions/function/invocations";

    /// <summary>
    /// Prefixo do Parameter Store (mesmo usado pela aplicação —
    /// AwsParameterStoreExtensions/ParameterStore:Path). Em Local aponta
    /// pro mesmo prefixo de produção, mas resolvido contra o LocalStack
    /// (ver ParameterStoreServiceUrl).
    /// </summary>
    public required string ParameterStorePath { get; init; }

    /// <summary>Nome da tabela DynamoDB do ambiente (fixo por convenção — não fica no Parameter Store).</summary>
    public required string DynamoDbTableName { get; init; }

    public string AwsRegion { get; init; } = "us-east-1";

    /// <summary>Só preenchido em Local — aponta os SDKs (SSM/Cognito/DynamoDB) pro LocalStack/cognito-local.</summary>
    public string? ParameterStoreServiceUrl { get; init; }
    public string? CognitoServiceUrl { get; init; }
    public string? DynamoDbServiceUrl { get; init; }
    public string? AwsAccessKey { get; init; }
    public string? AwsSecretKey { get; init; }

    public bool IsLocal => Mode == IntegrationTestMode.Local;

    private static IntegrationTestEnvironment Load()
    {
        var modeRaw = Environment.GetEnvironmentVariable("INTEGRATION_TESTS_MODE");

        return (modeRaw?.Trim().ToLowerInvariant()) switch
        {
            "hom" => new IntegrationTestEnvironment
            {
                Mode = IntegrationTestMode.Hom,
                BaseUrl = RequireEnv("INTEGRATION_TESTS_BASE_URL", "https://api-hom.jrnexpenses.com"),
                ParameterStorePath = RequireEnv("INTEGRATION_TESTS_PARAMETER_STORE_PATH", "/GastosApp/Hom/"),
                DynamoDbTableName = "GastosApp-Hom"
            },
            "prod" => new IntegrationTestEnvironment
            {
                Mode = IntegrationTestMode.Prod,
                BaseUrl = RequireEnv("INTEGRATION_TESTS_BASE_URL", "https://api.jrnexpenses.com"),
                ParameterStorePath = RequireEnv("INTEGRATION_TESTS_PARAMETER_STORE_PATH", "/GastosApp/"),
                DynamoDbTableName = "GastosApp"
            },
            // Default: local — permite rodar a suíte sem configurar nada
            // além de ter a Api rodando via `dotnet run`/Kestrel
            // (padrão mais comum: clicar Run/Debug no Test Explorer do
            // VS Code, cuja capacidade de injetar env vars custom não é
            // confiável o suficiente pra depender dela — achado real,
            // ver README.md). BaseUrl default é a Api local via Kestrel
            // (mesma porta de `.vscode/launch.json`, config
            // "GastosApp.Api" — 5049, lida de `Properties/launchSettings.json`
            // pelo VS Code). Só troca pro container Native AOT/Runtime
            // Interface Emulator se INTEGRATION_TESTS_TRANSPORT=rie for
            // setada explicitamente — usado por run-local.sh e pelas
            // configs "Debug Integration Tests (local, ...)" em
            // launch.json (ambos setam a env var no próprio processo
            // filho, mecanismo comprovadamente confiável, diferente de
            // configuração global do Test Explorer).
            _ => new IntegrationTestEnvironment
            {
                Mode = IntegrationTestMode.Local,
                BaseUrl = UseRieTransport()
                    ? null
                    : Environment.GetEnvironmentVariable("INTEGRATION_TESTS_BASE_URL") is { Length: > 0 } url
                        ? url
                        : "http://localhost:5049",
                ParameterStorePath = "/GastosApp/",
                DynamoDbTableName = "GastosApp-Local",
                ParameterStoreServiceUrl = "http://localhost:4566",
                CognitoServiceUrl = "http://localhost:9229",
                DynamoDbServiceUrl = "http://localhost:4566",
                AwsAccessKey = "test",
                AwsSecretKey = "test"
            }
        };
    }

    private static string RequireEnv(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static bool UseRieTransport() =>
        string.Equals(
            Environment.GetEnvironmentVariable("INTEGRATION_TESTS_TRANSPORT"),
            "rie",
            StringComparison.OrdinalIgnoreCase);
}
