using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Auth;
using GastosApp.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

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

        public static IServiceCollection AddCognitoAuth(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                //if (environment.IsDevelopment())
                //{
                //    options.RequireHttpsMetadata = false;
                //    options.TokenValidationParameters = new TokenValidationParameters
                //    {
                //        ValidateIssuer = false,
                //        ValidateAudience = false,
                //        ValidateIssuerSigningKey = false,
                //        ValidateLifetime = false,
                //        SignatureValidator = (token, _) =>
                //            new JwtSecurityTokenHandler().ReadJwtToken(token)
                //    };
                //}
                //else
                //{
                    // Leitura manual (sem .Get<T>()/reflection) — mesmo motivo
                    // documentado em AddCognitoSdk acima.
                    var cognitoSection = configuration.GetSection(CognitoOptions.SectionName);
                    var cognitoRegion = cognitoSection["Region"];
                    var cognitoUserPoolId = cognitoSection["UserPoolId"];
                    var cognitoClientId = cognitoSection["ClientId"];

                    options.RequireHttpsMetadata = true;
                    options.Authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudience = cognitoClientId,
                        ValidateLifetime = true
                    };
                //}

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
                        });
                    }
                };
            });

            return services;
        }
    }
}
