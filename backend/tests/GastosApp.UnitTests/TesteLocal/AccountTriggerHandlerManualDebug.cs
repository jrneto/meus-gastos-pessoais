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
// Antes de rodar (por qualquer um dos dois jeitos abaixo): ajuste userId
// pro sub do usuário que você confirmou localmente (ver
// ConfirmationCode/estado em /app/.cognito/db/<user-pool-id>.json, dentro
// do container gastosapp-cognito-local) — email fica fixo em
// titular@jrnexpenses.com, mesmo domínio de teste já usado pela suíte de
// integração (nunca commitar e-mail pessoal aqui).
//
// Rodar (Test Explorer do VS Code): botão direito no teste → "Debug Test"
// — quando o Test Explorer estiver enxergando os testes (às vezes some,
// ver alternativa abaixo).
//
// Rodar (linha de comando, quando o Test Explorer não aparece): tirar o
// "Skip" do [Fact] abaixo primeiro — Skip nunca executa o corpo do
// método, então nenhum breakpoint é atingido com ele presente. Depois,
// a partir de backend/:
//
//   VSTEST_HOST_DEBUG=1 dotnet test tests/GastosApp.UnitTests \
//     --filter "FullyQualifiedName~AccountTriggerHandlerManualDebug"
//
// O testhost builda, imprime "Process Id: <PID>, Name: testhost" e PARA,
// esperando debugger anexado (não roda o teste ainda). No VS Code:
// Ctrl+Shift+P → ".NET: Attach to a .NET 5+ or .NET Core process" →
// escolher esse PID (filtrar digitando "testhost" ajuda a achar). A
// execução só continua depois de anexado — os breakpoints já colocados
// no arquivo são atingidos normalmente a partir daí. Ao terminar, devolva
// o "Skip" pro [Fact] (evita rodar isso à toa em qualquer `dotnet test`
// sem --filter).
public class AccountTriggerHandlerManualDebug
{
    [Fact(Skip = "Manual — rode com Debug Test quando precisar simular o trigger PostConfirmation localmente.")]
    public async Task Simular_PostConfirmation_ParaUsuarioLocal()
    {
        // AJUSTE AQUI: sub do usuário que você confirmou localmente.
        const string userId = "bef5807d-2126-4624-accf-6ab34e2bfca6";
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
