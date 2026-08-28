namespace GastosApp.IntegrationTests.Support;

public static class ApiTransportFactory
{
    /// <summary>
    /// Cria o transporte certo pro ambiente atual (<see cref="IntegrationTestEnvironment.Current"/>).
    /// O chamador é responsável por descartar o resultado (ambas as
    /// implementações possuem <see cref="IDisposable"/>).
    /// </summary>
    public static IApiTransport Create(IntegrationTestEnvironment? environment = null)
    {
        environment ??= IntegrationTestEnvironment.Current;

        // Local + BaseUrl setada (INTEGRATION_TESTS_BASE_URL) = "local
        // direto": a Api roda via `dotnet run`/Kestrel (JIT, debugável
        // com breakpoint normal) em vez do container Native AOT/RIE —
        // Cognito/DynamoDB continuam via LocalStack/cognito-local (isso
        // não depende do transporte, é resolvido à parte em
        // AwsClientFactory a partir do mesmo Mode=Local). Ver
        // IntegrationTestEnvironment.BaseUrl e o README do projeto.
        if (environment.Mode == IntegrationTestMode.Local && environment.BaseUrl is not null)
            return new DirectHttpTransport(environment.BaseUrl);

        return environment.Mode switch
        {
            IntegrationTestMode.Local => new LambdaRieTransport(environment.RieInvokeUrl),
            _ => new DirectHttpTransport(environment.BaseUrl
                ?? throw new InvalidOperationException($"BaseUrl não configurada para o modo {environment.Mode}."))
        };
    }
}
