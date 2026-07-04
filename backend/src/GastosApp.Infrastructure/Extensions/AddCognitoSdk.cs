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
            // Bind das configs para IOptions<CognitoOptions>
            services.Configure<CognitoOptions>(
                configuration.GetSection(CognitoOptions.SectionName));

            services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CognitoOptions>>().Value;

                // Debug temporário — remove depois
                Console.WriteLine($"[DEBUG] Region: '{options.Region}'");
                Console.WriteLine($"[DEBUG] UserPoolId: '{options.UserPoolId}'");
                Console.WriteLine($"[DEBUG] ClientId: '{options.ClientId}'");

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
                    var cognitoOptions = configuration.GetSection(CognitoOptions.SectionName).Get<CognitoOptions>();

                    options.RequireHttpsMetadata = true;
                    options.Authority = $"https://cognito-idp.{cognitoOptions!.Region}.amazonaws.com/{cognitoOptions.UserPoolId}";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudience = cognitoOptions.ClientId,
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
