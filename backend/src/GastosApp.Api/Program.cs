using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.Lambda.Serialization.SystemTextJson;
using GastosApp.Api.Common;
using GastosApp.Api.Endpoints;
using GastosApp.Api.Middlewares;
using GastosApp.Application.DependencyInjection;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsEnvironment("Testing"))
{
    var parameterStoreSection = builder.Configuration.GetSection("ParameterStore");
    var parameterStorePath = parameterStoreSection["Path"] ?? "/GastosApp/";

    // ServiceURL/AccessKey/SecretKey são só para dev local (LocalStack) —
    // ver FEAT-18. Em produção/homologação essas chaves não existem, e o
    // comportamento continua sendo SSM real com credenciais do ambiente.
    builder.Configuration.AddAwsParameterStore(
        parameterStorePath,
        parameterStoreSection["ServiceURL"],
        parameterStoreSection["Region"] ?? "us-east-1",
        parameterStoreSection["AccessKey"],
        parameterStoreSection["SecretKey"]);
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Contexto de serialização source-generated, obrigatório em Native AOT
// (reflection-based System.Text.Json lança em runtime para tipos
// desconhecidos — ver AppJsonSerializerContext).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddCognitoAuth(builder.Configuration);
builder.Services.AddAuthorization();

// Origens do frontend liberadas via configuração: "Cors:AllowedOrigins"
// (appsettings.Development.json em dev local) + "Cors:ProductionOrigins"
// (só Parameter Store, produção) — chaves separadas de propósito: as
// duas convivem no mesmo Parameter Store /GastosApp/, lido em todo
// ambiente (inclusive dev local), então uma chave só de produção evita
// que o valor de produção sobrescreva o localhost de dev.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var productionOrigins = builder.Configuration.GetSection("Cors:ProductionOrigins").Get<string[]>() ?? [];
var corsOrigins = allowedOrigins.Concat(productionOrigins).ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Necessário para o cookie httpOnly de refresh token (FEAT-15)
        // ser enviado pelo navegador em `credentials: 'include'`. Só é
        // válido combinado com WithOrigins (lista explícita) — nunca
        // com AllowAnyOrigin, que o ASP.NET Core rejeita em runtime
        // junto de AllowCredentials.
        .AllowCredentials());
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddScoped<CurrentAccountContext>();
builder.Services.AddAWSLambdaHosting(
    LambdaEventSource.HttpApi,
    new SourceGeneratorLambdaJsonSerializer<LambdaEventJsonSerializerContext>());

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapExpenseEndpoints();
app.MapCategoryEndpoints();
app.MapMemberEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program { }