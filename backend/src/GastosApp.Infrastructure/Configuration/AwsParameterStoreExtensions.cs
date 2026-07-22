using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace GastosApp.Infrastructure.Configuration
{
    public static class AwsParameterStoreExtensions
    {
        private const string ParameterPath = "/GastosApp/";

        // Leitura direta via AWSSDK.SimpleSystemsManagement (mesmo padrão já
        // usado com sucesso pelos clientes de DynamoDB/Cognito) em vez da
        // lib de terceiros Amazon.Extensions.Configuration.SystemsManager —
        // achado durante a implementação da FEAT-10: essa lib retornava
        // configuração vazia dentro da Lambda (Native AOT), sem lançar
        // nenhum erro visível, deixando CognitoOptions inteiramente nulo.
        public static IConfigurationBuilder AddAwsParameterStore(
            this IConfigurationBuilder builder)
        {
            using var client = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.USEast1);

            var values = new Dictionary<string, string?>();
            string? nextToken = null;

            do
            {
                var response = client.GetParametersByPathAsync(new GetParametersByPathRequest
                {
                    Path = ParameterPath,
                    Recursive = true,
                    WithDecryption = true,
                    NextToken = nextToken
                }).GetAwaiter().GetResult();

                foreach (var parameter in response.Parameters)
                {
                    var key = parameter.Name[ParameterPath.Length..].Replace('/', ':');
                    values[key] = parameter.Value;
                }

                nextToken = response.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            if (values.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Nenhum parâmetro encontrado em '{ParameterPath}' no Parameter Store.");
            }

            return builder.AddInMemoryCollection(values);
        }
    }
}
