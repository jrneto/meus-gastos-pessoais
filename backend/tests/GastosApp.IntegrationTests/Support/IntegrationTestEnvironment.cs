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

    /// <summary>URL base da API — só usada em Hom/Prod (DirectHttpTransport).</summary>
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
            // além de `docker compose up -d` + run-local.sh (FEAT-18/FEAT-29).
            _ => new IntegrationTestEnvironment
            {
                Mode = IntegrationTestMode.Local,
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
}
