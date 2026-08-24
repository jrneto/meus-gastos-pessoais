using System.Text.Json;
using GastosApp.Application.Accounts.Commands.EnsureAccount;
using Mediator;
using Microsoft.Extensions.Logging;

namespace GastosApp.CognitoTriggers;

// Lógica do handler extraída de Function.cs pra ficar testável sem subir
// o runtime do Lambda (LambdaBootstrapBuilder não dá pra invocar em
// ComponentTest/UnitTest). Function.cs fica só com o bootstrap (DI +
// LambdaBootstrapBuilder), delegando pra cá.
public static class AccountTriggerHandler
{
    public static async Task<CognitoPostConfirmationEvent> HandleAsync(
        CognitoPostConfirmationEvent evt,
        ISender sender,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (evt.Request.UserAttributes.TryGetValue("sub", out var userId) && !string.IsNullOrEmpty(userId))
        {
            try
            {
                await sender.Send(new EnsureAccountCommand(userId), cancellationToken);
            }
            catch (Exception ex)
            {
                // Nunca propaga: o Post Confirmation trigger é invocado de forma
                // síncrona como parte da própria chamada ConfirmSignUp/
                // AdminConfirmSignUp/ConfirmForgotPassword — se este handler
                // lançar, a confirmação falha pro usuário. Falha transitória
                // aqui nunca pode bloquear a confirmação (ver spec.md, decisão
                // técnica 2 do plan.md) — só loga; o fallback do login cobre.
                logger.LogError(
                    ex, "Falha ao garantir Account para o usuário {UserId} no trigger PostConfirmation.", userId);
            }
        }

        // Temporário (remover depois de confirmar em homologação, ver
        // spec.md/plan.md da FEAT-19): loga o JSON exato que sai pro
        // Cognito, serializado com o mesmo contexto/opções que a
        // LambdaBootstrapBuilder usa de verdade — depois de 3 rodadas de
        // InvalidLambdaResponseException corrigindo campo por campo por
        // inferência da doc, próximo passo é comparar o byte a byte real
        // em vez de continuar adivinhando.
        // JsonTypeInfo gerado (não o overload com JsonSerializerOptions) —
        // este projeto é Native AOT, e o overload genérico usa reflection
        // (quebra silenciosamente ou no trimming; ver GastosApp.CognitoTriggers.csproj).
        logger.LogInformation(
            "Resposta que será devolvida ao Cognito: {ResponseJson}",
            JsonSerializer.Serialize(evt, CognitoTriggerJsonSerializerContext.Default.CognitoPostConfirmationEvent));

        return evt; // Cognito exige o evento de volta, alterado ou não
    }
}
