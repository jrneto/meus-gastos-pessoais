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
                    SenderEmail = section["SenderEmail"]!
                };

                return Options.Create(options);
            });

            // Sem seção própria de região: reaproveita CognitoOptions.Region
            // (mesma região AWS de todo o projeto).
            services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
            {
                var region = sp.GetRequiredService<IOptions<CognitoOptions>>().Value.Region;
                return new AmazonSimpleEmailServiceV2Client(Amazon.RegionEndpoint.GetBySystemName(region));
            });

            services.AddScoped<IEmailSender, SesEmailService>();
            services.AddScoped<IPasswordChangedEmailSender, SesPasswordChangedEmailSender>();

            return services;
        }
    }
}
