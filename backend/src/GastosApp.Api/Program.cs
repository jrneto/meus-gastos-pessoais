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
    builder.Configuration.AddAwsParameterStore();
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

builder.Services.AddAuthorization();

// Origens do frontend liberadas via configuração ("Cors:AllowedOrigins"),
// alimentada por appsettings.Development.json em dev local e por
// Parameter Store em produção — evita hardcode do domínio do frontend.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
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

app.Run();

public partial class Program { }