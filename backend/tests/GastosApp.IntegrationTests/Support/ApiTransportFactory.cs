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

        return environment.Mode switch
        {
            IntegrationTestMode.Local => new LambdaRieTransport(environment.RieInvokeUrl),
            _ => new DirectHttpTransport(environment.BaseUrl
                ?? throw new InvalidOperationException($"BaseUrl não configurada para o modo {environment.Mode}."))
        };
    }
}
