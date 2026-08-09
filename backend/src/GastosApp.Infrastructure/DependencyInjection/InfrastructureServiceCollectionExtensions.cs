using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using GastosApp.Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddAwsInfrastructure(configuration, environment);

            services.AddScoped<IExpenseRepository, DynamoDbExpenseRepository>();

            return services;
        }

        /// <summary>
        /// Injeta os clientes SDK da AWS que a aplicação vai usar para se comunicar com a nuvem.
        /// </summary>
        public static IServiceCollection AddAwsInfrastructure(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            var regionStr = configuration["Cognito:Region"] ?? "us-east-1";
            var region = RegionEndpoint.GetBySystemName(regionStr);

            services.AddCognitoSdk(configuration);

            // Leitura manual (sem Configure<T>()/reflection) — mesmo motivo
            // documentado em AddCognitoSdk: Configure<T>(IConfiguration) usa
            // reflection para popular as propriedades e falha silenciosamente
            // sob Native AOT (sem lançar exceção, só não preenche nada).
            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(DynamoDbOptions.SectionName);
                var options = new DynamoDbOptions
                {
                    TableName = section["TableName"] ?? "GastosApp",
                    Region = section["Region"] ?? "us-east-1"
                };

                return Options.Create(options);
            });

            services.AddSingleton<IAmazonDynamoDB>(sp =>
            {
                var dynamoDbRegionStr = configuration["DynamoDb:Region"] ?? regionStr;
                var dynamoDbRegion = RegionEndpoint.GetBySystemName(dynamoDbRegionStr);
                return new AmazonDynamoDBClient(dynamoDbRegion);
            });

            return services;
        }
    }
}
