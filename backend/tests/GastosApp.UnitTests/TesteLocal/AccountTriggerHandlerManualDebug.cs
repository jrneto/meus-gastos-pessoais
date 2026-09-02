using GastosApp.Application.DependencyInjection;
using GastosApp.CognitoTriggers;
using GastosApp.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mediator;

namespace GastosApp.UnitTests.TesteLocal;

// MANUAL — não faz parte da suíte automatizada, não roda em CI (Skip
// fixo abaixo). Existe pra simular o trigger PostConfirmation do
// Cognito localmente: o cognito-local não invoca Lambda nenhuma (ver
// backend/infra/scripts/init-cognito.sh — sem LambdaConfig configurado
// no User Pool), então esse é o único jeito de exercitar
// AccountTriggerHandler sem deploy. Chama HandleAsync direto (extraído
// de Function.cs exatamente pra ser testável assim, ver comentário lá)
// contra o LocalStack já rodando via `docker compose up -d`
// (backend/infra/). Não usa RIE/Docker/Native AOT — não pega bug
// específico de AOT, só a lógica de negócio real.
//
// Pré-requisito: LocalStack no ar (gastosapp-localstack) com a tabela
// GastosApp-Local já seedada (./infra/scripts/local-init.sh).
//
// Rodar: Test Explorer do VS Code → botão direito no teste → "Debug Test"
// (breakpoint funciona normal dentro de AccountTriggerHandler.HandleAsync
// e em qualquer coisa que EnsureAccountCommand chame). Antes de rodar,
// ajuste userId pro sub do usuário que você confirmou localmente (ver
// ConfirmationCode/estado em /app/.cognito/db/<user-pool-id>.json, dentro
// do container gastosapp-cognito-local) — email fica fixo em
// titular@jrnexpenses.com, mesmo domínio de teste já usado pela suíte de
// integração (nunca commitar e-mail pessoal aqui).
public class AccountTriggerHandlerManualDebug
{
    [Fact(Skip = "Manual — rode com Debug Test quando precisar simular o trigger PostConfirmation localmente.")]
    public async Task Simular_PostConfirmation_ParaUsuarioLocal()
    {
        // AJUSTE AQUI: sub do usuário que você confirmou localmente.
        const string userId = "b63bc2af-05dd-4655-82a3-09d6af1740b8";
        const string email = "titular@jrnexpenses.com";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDb:TableName"] = "GastosApp-Local",
                ["DynamoDb:Region"] = "us-east-1",
                ["DynamoDb:ServiceURL"] = "http://localhost:4566",
                ["DynamoDb:AccessKey"] = "test",
                ["DynamoDb:SecretKey"] = "test",
                // Cognito não é usado por este handler, mas AddInfrastructure
                // registra o client mesmo assim — valores dummy bastam.
                ["Cognito:Region"] = "us-east-1",
                ["Cognito:UserPoolId"] = "local_6Enm3gxX",
                ["Cognito:ClientId"] = "dummy",
                ["Cognito:ServiceURL"] = "http://localhost:9229",
                ["Cognito:AccessKey"] = "test",
                ["Cognito:SecretKey"] = "test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddConsole());
        services.AddApplicationServices();
        services.AddInfrastructure(configuration, new ManualDebugHostEnvironment());

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var logger = provider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AccountTriggerHandlerManualDebug");

        // Mesmo formato que o Cognito real manda pro PostConfirmation —
        // ver CognitoPostConfirmationEvent.cs pro porquê de cada campo.
        var evt = new CognitoPostConfirmationEvent
        {
            Version = "1",
            Region = "us-east-1",
            UserPoolId = "local_6Enm3gxX",
            UserName = userId,
            TriggerSource = "PostConfirmation_ConfirmSignUp",
            Request = new CognitoPostConfirmationRequest
            {
                UserAttributes = new Dictionary<string, string>
                {
                    ["sub"] = userId,
                    ["email"] = email
                }
            }
        };

        // 👉 Coloque um breakpoint na linha abaixo (ou dentro de
        // AccountTriggerHandler.HandleAsync) e rode em modo Debug.
        var result = await AccountTriggerHandler.HandleAsync(evt, sender, logger, CancellationToken.None);

        logger.LogInformation("Trigger simulado com sucesso para {UserId}.", userId);
    }
}

file sealed class ManualDebugHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "GastosApp.UnitTests.TesteLocal";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
