using Amazon.Runtime;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace GastosApp.Infrastructure.Configuration
{
    public static class AwsParameterStoreExtensions
    {
        // Leitura direta via AWSSDK.SimpleSystemsManagement (mesmo padrão já
        // usado com sucesso pelos clientes de DynamoDB/Cognito) em vez da
        // lib de terceiros Amazon.Extensions.Configuration.SystemsManager —
        // achado durante a implementação da FEAT-10: essa lib retornava
        // configuração vazia dentro da Lambda (Native AOT), sem lançar
        // nenhum erro visível, deixando CognitoOptions inteiramente nulo.
        //
        // `path` é configurável (default igual ao valor fixo original) para
        // permitir isolamento entre ambientes (produção lê "/GastosApp/",
        // homologação lê "/GastosApp/Hom/" via variável de ambiente da
        // Lambda) — ver FEAT-13.
        //
        // `serviceURL`/`region`/`accessKey`/`secretKey` são só para dev
        // local (LocalStack) — ver FEAT-18. Quando omitidos, comportamento
        // idêntico ao anterior (SSM real em us-east-1, credenciais do
        // ambiente/IAM Role).
        public static IConfigurationBuilder AddAwsParameterStore(
            this IConfigurationBuilder builder,
            string path = "/GastosApp/",
            string? serviceURL = null,
            string region = "us-east-1",
            string? accessKey = null,
            string? secretKey = null)
        {
            var config = new AmazonSimpleSystemsManagementConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
            };

            if (!string.IsNullOrEmpty(serviceURL))
            {
                config.ServiceURL = serviceURL;
                config.AuthenticationRegion = region;
            }

            using var client = !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
                ? new AmazonSimpleSystemsManagementClient(new BasicAWSCredentials(accessKey, secretKey), config)
                : new AmazonSimpleSystemsManagementClient(config); // usa IAM Role / credenciais do ambiente

            var values = new Dictionary<string, string?>();
            string? nextToken = null;

            do
            {
                var response = client.GetParametersByPathAsync(new GetParametersByPathRequest
                {
                    Path = path,
                    Recursive = true,
                    WithDecryption = true,
                    NextToken = nextToken
                }).GetAwaiter().GetResult();

                foreach (var parameter in response.Parameters)
                {
                    var key = parameter.Name[path.Length..].Replace('/', ':');
                    values[key] = parameter.Value;
                }

                nextToken = response.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            if (values.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Nenhum parâmetro encontrado em '{path}' no Parameter Store.");
            }

            return builder.AddInMemoryCollection(values);
        }
    }
}
