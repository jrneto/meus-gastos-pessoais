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
            services.AddCognitoAuth(configuration, environment);

            services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));

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
