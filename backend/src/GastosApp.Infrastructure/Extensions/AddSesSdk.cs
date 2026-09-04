using Amazon.SimpleEmailV2;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Extensions
{
    public static class InfraEmailExtensions
    {
        public static IServiceCollection AddSesSdk(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Leitura manual (sem Configure<T>()/reflection) — mesmo motivo
            // documentado em AddCognitoSdk: falha silenciosamente sob Native AOT.
            services.AddSingleton(_ =>
            {
                var section = configuration.GetSection(SesOptions.SectionName);
                var options = new SesOptions
                {
                    SenderEmail = section["SenderEmail"]!,
                    Region = section["Region"] ?? "us-east-1"
                };

                return Options.Create(options);
            });

            // Região própria de SesOptions (com fallback seguro) — não reaproveita
            // mais CognitoOptions.Region: essa dependência quebrava a Lambda de
            // trigger de conta, que nunca configura Cognito de propósito (ver
            // SesOptions.Region para o histórico do bug).
            services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
            {
                var region = sp.GetRequiredService<IOptions<SesOptions>>().Value.Region;
                return new AmazonSimpleEmailServiceV2Client(Amazon.RegionEndpoint.GetBySystemName(region));
            });

            services.AddScoped<IEmailSender, SesEmailService>();
            services.AddScoped<IPasswordChangedEmailSender, SesPasswordChangedEmailSender>();
            services.AddScoped<IWelcomeEmailSender, SesWelcomeEmailSender>();

            return services;
        }
    }
}
