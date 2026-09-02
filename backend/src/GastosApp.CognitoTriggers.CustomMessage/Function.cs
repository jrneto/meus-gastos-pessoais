using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using GastosApp.CognitoTriggers.CustomMessage;
using Microsoft.Extensions.Logging;

// Composition root deste Lambda — bem mais simples que o de
// GastosApp.CognitoTriggers: não há repositório/Mediator a resolver (este
// trigger só formata texto a partir do próprio evento do Cognito), então um
// ILoggerFactory avulso é suficiente, sem ServiceCollection (ver plan.md,
// decisão técnica 6).
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("GastosApp.CognitoTriggers.CustomMessage");

var handler = (CognitoCustomMessageEvent evt, ILambdaContext context) =>
    CustomMessageTriggerHandler.HandleAsync(evt, logger, CancellationToken.None);

// Mesmo gotcha da FEAT-19: o construtor sem parâmetros de
// SourceGeneratorLambdaJsonSerializer<T> ignora o CamelCase configurado via
// [JsonSourceGenerationOptions] — só o overload com Action<JsonSerializerOptions>
// aplica a policy que passamos (ver Function.cs de GastosApp.CognitoTriggers).
await LambdaBootstrapBuilder.Create(
        handler,
        new SourceGeneratorLambdaJsonSerializer<CognitoCustomMessageJsonSerializerContext>(
            options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
    .Build()
    .RunAsync();
