using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Auth;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Extensions
{
    public static class InfraAuthExtensions
    {
        public static IServiceCollection AddCognitoSdk(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Leitura manual (sem ConfigurationBinder/reflection) — achado
            // durante a implementação da FEAT-10: Configure<T>(IConfiguration)
            // usa reflection para popular as propriedades e falha
            // silenciosamente sob Native AOT (sem lançar exceção, só não
            // preenche nada).
            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(CognitoOptions.SectionName);
                var options = new CognitoOptions
                {
                    Region = section["Region"]!,
                    UserPoolId = section["UserPoolId"]!,
                    ClientId = section["ClientId"]!,
                    ServiceURL = section["ServiceURL"],
                    AccessKey = section["AccessKey"],
                    SecretKey = section["SecretKey"]
                };

                return Options.Create(options);
            });

            services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CognitoOptions>>().Value;

                var config = new AmazonCognitoIdentityProviderConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region)
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
                    return new AmazonCognitoIdentityProviderClient(credentials, config);
                }

                return new AmazonCognitoIdentityProviderClient(config); // usa IAM Role / credenciais do ambiente
            });

            services.AddScoped<IAuthService, CognitoAuthService>();

            return services;
        }
    }
}
