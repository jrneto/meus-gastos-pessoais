using Amazon;
using Amazon.CognitoIdentityProvider;
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

            //services.AddScoped<ICustomerRepository, CustomerRepository>();

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

            // No futuro, suasinjeções do DynamoDB entrarão aqui de forma isolada:
            // services.AddSingleton<IAmazonDynamoDB>(...);

            return services;
        }
    }
}
