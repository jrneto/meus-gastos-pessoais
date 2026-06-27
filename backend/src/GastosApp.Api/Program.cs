using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Amazon.CognitoIdentityProvider;
using GastosApp.Api.Endpoints;
using GastosApp.Api.Middlewares;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddOpenApi();

// Tratamento global de erros
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Carregar segredos em produção via AWS Secrets Manager
if (!builder.Environment.IsDevelopment())
{
    try
    {
        var secretName = builder.Configuration["AWS:SecretName"] ?? "GastosApp/Production/Secrets";
        var region = builder.Configuration["AWS:Region"] ?? "us-east-1";

        using var secretsClient = new Amazon.SecretsManager.AmazonSecretsManagerClient(
            Amazon.RegionEndpoint.GetBySystemName(region));

        var response = await secretsClient.GetSecretValueAsync(new Amazon.SecretsManager.Model.GetSecretValueRequest
        {
            SecretId = secretName
        });

        if (!string.IsNullOrEmpty(response.SecretString))
        {
            var secrets = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
            if (secrets != null)
            {
                foreach (var kvp in secrets)
                {
                    builder.Configuration[$"AWS:{kvp.Key}"] = kvp.Value;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao carregar segredos do Secrets Manager: {ex.Message}");
    }
}

// Configuração do JWT Bearer Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = false,
            // Bypass da validação de assinatura em Desenvolvimento
            SignatureValidator = delegate (string token, TokenValidationParameters _)
            {
                var jwtHandler = new JwtSecurityTokenHandler();
                return jwtHandler.ReadJwtToken(token);
            }
        };
    }
    else
    {
        var region = builder.Configuration["AWS:Region"] ?? "us-east-1";
        var userPoolId = builder.Configuration["AWS:UserPoolId"];
        var clientId = builder.Configuration["AWS:ClientId"];

        options.RequireHttpsMetadata = true;
        options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true
        };
    }

    // Customização do retorno 401 para seguir ProblemDetails (RFC 9457)
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Type = "https://gastosapp.dev/errors/unauthorized"
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    };
});

builder.Services.AddAuthorization();

// Configuração do Cognito SDK
builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var serviceUrl = configuration["AWS:ServiceURL"];
    var region = configuration["AWS:Region"] ?? "us-east-1";
    var accessKey = configuration["AWS:AccessKey"] ?? "test";
    var secretKey = configuration["AWS:SecretKey"] ?? "test";

    var config = new AmazonCognitoIdentityProviderConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
    };

    if (!string.IsNullOrEmpty(serviceUrl))
    {
        config.ServiceURL = serviceUrl;
        config.AuthenticationRegion = region;
    }

    var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
    return new AmazonCognitoIdentityProviderClient(credentials, config);
});

// Registro das dependências das camadas de Application e Infrastructure
builder.Services.AddScoped<IAuthService, CognitoAuthService>();
builder.Services.AddScoped<RegisterUserCommandHandler>();
builder.Services.AddScoped<LoginUserCommandHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Mapeamento dos endpoints de autenticação
app.MapAuthEndpoints();

app.Run();
