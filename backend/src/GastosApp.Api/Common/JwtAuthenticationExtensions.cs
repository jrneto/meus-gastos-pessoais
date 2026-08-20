using GastosApp.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace GastosApp.Api.Common;

// Configuração do middleware de autenticação JWT (validação contra o JWKS
// do Cognito) e da resposta 401 formatada como ProblemDetails. Migrado de
// GastosApp.Infrastructure (InfraAuthExtensions.AddCognitoAuth) — é
// configuração de pipeline HTTP/apresentação (a Api "recebe request,
// valida JWT", conforme backend/docs/constitution.md), não integração
// com um sistema externo, então pertence à Api, não à Infrastructure.
// Isso também permite referenciar o AppJsonSerializerContext (Native AOT)
// sem inverter a direção de dependência Infrastructure → Api.
public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddCognitoAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Leitura manual (sem .Get<T>()/reflection) — mesmo motivo
            // documentado em AddCognitoSdk: Configure<T>(IConfiguration) usa
            // reflection para popular as propriedades e falha silenciosamente
            // sob Native AOT.
            var cognitoSection = configuration.GetSection(CognitoOptions.SectionName);
            var cognitoRegion = cognitoSection["Region"];
            var cognitoUserPoolId = cognitoSection["UserPoolId"];
            var cognitoClientId = cognitoSection["ClientId"];
            var cognitoServiceURL = cognitoSection["ServiceURL"];

            // ServiceURL só é definido em dev local (cognito-local, ver
            // FEAT-18) — nesse caso o JWKS é servido em HTTP puro, e o
            // Authority aponta pro emulador em vez do domínio real do
            // Cognito. Produção/homologação não declaram essa chave, então
            // o comportamento real fica inalterado.
            options.RequireHttpsMetadata = string.IsNullOrEmpty(cognitoServiceURL);
            options.Authority = !string.IsNullOrEmpty(cognitoServiceURL)
                ? $"{cognitoServiceURL}/{cognitoUserPoolId}"
                : $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = cognitoClientId,
                ValidateLifetime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Não autorizado",
                        Type = "https://gastosapp.dev/errors/unauthorized"
                    }, AppJsonSerializerContext.Default.ProblemDetails);
                }
            };
        });

        return services;
    }
}
