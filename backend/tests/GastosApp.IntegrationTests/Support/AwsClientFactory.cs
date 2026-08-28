using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Constrói os clientes AWS usados só pelos utilitários administrativos
/// de setup/cleanup (nunca pelo fluxo de negócio testado). Mesmo padrão
/// de <c>InfrastructureServiceCollectionExtensions</c>/<c>AddCognitoSdk</c>:
/// em Local aponta pro LocalStack/cognito-local com credenciais dummy;
/// em Hom/Prod usa a cadeia de credenciais padrão do ambiente (role OIDC
/// já assumida pelo job de CI via aws-actions/configure-aws-credentials,
/// ou o perfil AWS local do desenvolvedor).
/// </summary>
public static class AwsClientFactory
{
    public static IAmazonSimpleSystemsManagement CreateSsmClient(IntegrationTestEnvironment env)
    {
        var config = new AmazonSimpleSystemsManagementConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(env.AwsRegion) };

        if (!string.IsNullOrEmpty(env.ParameterStoreServiceUrl))
        {
            config.ServiceURL = env.ParameterStoreServiceUrl;
            config.AuthenticationRegion = env.AwsRegion;
            return new AmazonSimpleSystemsManagementClient(DummyCredentials(env), config);
        }

        return new AmazonSimpleSystemsManagementClient(config);
    }

    public static IAmazonCognitoIdentityProvider CreateCognitoClient(IntegrationTestEnvironment env)
    {
        var config = new AmazonCognitoIdentityProviderConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(env.AwsRegion) };

        if (!string.IsNullOrEmpty(env.CognitoServiceUrl))
        {
            config.ServiceURL = env.CognitoServiceUrl;
            config.AuthenticationRegion = env.AwsRegion;
            return new AmazonCognitoIdentityProviderClient(DummyCredentials(env), config);
        }

        return new AmazonCognitoIdentityProviderClient(config);
    }

    public static IAmazonDynamoDB CreateDynamoDbClient(IntegrationTestEnvironment env)
    {
        var config = new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(env.AwsRegion) };

        if (!string.IsNullOrEmpty(env.DynamoDbServiceUrl))
        {
            config.ServiceURL = env.DynamoDbServiceUrl;
            config.AuthenticationRegion = env.AwsRegion;
            return new AmazonDynamoDBClient(DummyCredentials(env), config);
        }

        return new AmazonDynamoDBClient(config);
    }

    private static BasicAWSCredentials DummyCredentials(IntegrationTestEnvironment env) =>
        new(env.AwsAccessKey ?? "test", env.AwsSecretKey ?? "test");
}
