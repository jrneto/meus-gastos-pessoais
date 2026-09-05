using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Accounts;
using GastosApp.Infrastructure.Categories;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Extensions;
using GastosApp.Infrastructure.Members;
using GastosApp.Infrastructure.Transactions;
using GastosApp.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using LoggingOptions = GastosApp.Infrastructure.Configuration.LoggingOptions; // desambigua de Amazon.LoggingOptions (using Amazon; acima)

namespace GastosApp.Infrastructure.DependencyInjection
{
    public static class InfrastructureServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddAwsInfrastructure(configuration, environment);

            services.AddScoped<ITransactionRepository, DynamoDbTransactionRepository>();
            services.AddScoped<ICategoryRepository, DynamoDbCategoryRepository>();
            services.AddScoped<IAccountRepository, DynamoDbAccountRepository>();
            services.AddScoped<IMembershipRepository, DynamoDbMembershipRepository>();
            services.AddScoped<IUserProfileRepository, DynamoDbUserProfileRepository>();

            return services;
        }

        /// <summary>
        /// Injeta os clientes SDK da AWS que a aplicação vai usar para se comunicar com a nuvem.
        /// </summary>
        public static IServiceCollection AddAwsInfrastructure(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddCognitoSdk(configuration);
            services.AddSesSdk(configuration);

            // Leitura manual (sem Configure<T>()/reflection) — mesmo motivo
            // documentado em AddCognitoSdk: Configure<T>(IConfiguration) usa
            // reflection para popular as propriedades e falha silenciosamente
            // sob Native AOT (sem lançar exceção, só não preenche nada).
            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(LoggingOptions.SectionName);
                var options = new LoggingOptions
                {
                    FullPayloadLoggingEnabled = section["FullPayloadLoggingEnabled"] == "true"
                };

                return Options.Create(options);
            });

            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(DynamoDbOptions.SectionName);
                var options = new DynamoDbOptions
                {
                    TableName = section["TableName"] ?? "GastosApp",
                    Region = section["Region"] ?? "us-east-1",
                    ServiceURL = section["ServiceURL"],
                    AccessKey = section["AccessKey"],
                    SecretKey = section["SecretKey"]
                };

                return Options.Create(options);
            });

            services.AddSingleton<IAmazonDynamoDB>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<DynamoDbOptions>>().Value;

                var config = new AmazonDynamoDBConfig
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
                };

                if (!string.IsNullOrEmpty(options.ServiceURL))
                {
                    config.ServiceURL = options.ServiceURL;
                    config.AuthenticationRegion = options.Region;
                }

                // Em produção na AWS, use IAM Role — sem AccessKey/SecretKey hardcoded
                if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
                {
                    var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
                    return new AmazonDynamoDBClient(credentials, config);
                }

                return new AmazonDynamoDBClient(config); // usa IAM Role / credenciais do ambiente
            });

            return services;
        }
    }
}
