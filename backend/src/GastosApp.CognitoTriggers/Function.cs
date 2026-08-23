using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using GastosApp.Application.DependencyInjection;
using GastosApp.CognitoTriggers;
using GastosApp.Infrastructure.DependencyInjection;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Composition root deste Lambda — espelha o que GastosApp.Api/Program.cs
// já faz (AddApplicationServices + AddInfrastructure), mas sem hospedagem
// ASP.NET Core: aqui é só um handler de evento do Cognito.
//
// Configuração só de variável de ambiente (DynamoDb__TableName etc.) —
// decidido em conversa: este Lambda não lê Parameter Store (sem
// AddAwsParameterStore), pra não precisar de ssm:GetParametersByPath na
// IAM Role nem de uma chamada de rede a mais no cold start.
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

var services = new ServiceCollection();
services.AddLogging(logging => logging.AddConsole()); // CloudWatch Logs captura stdout/stderr da Lambda
services.AddApplicationServices();
services.AddInfrastructure(configuration, new LambdaHostEnvironment());

var provider = services.BuildServiceProvider();
var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

var handler = async (CognitoPostConfirmationEvent evt, ILambdaContext context) =>
{
    // Escopo por invocação — mesmo padrão de vida útil (Scoped) que os
    // repositórios/Mediator já assumem no resto do projeto.
    using var scope = provider.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    var logger = loggerFactory.CreateLogger("GastosApp.CognitoTriggers.AccountTrigger");

    return await AccountTriggerHandler.HandleAsync(evt, sender, logger, CancellationToken.None);
};

await LambdaBootstrapBuilder.Create(
        handler,
        new SourceGeneratorLambdaJsonSerializer<CognitoTriggerJsonSerializerContext>())
    .Build()
    .RunAsync();
