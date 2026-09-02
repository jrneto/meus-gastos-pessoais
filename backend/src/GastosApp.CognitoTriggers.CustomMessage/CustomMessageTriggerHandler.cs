using Microsoft.Extensions.Logging;

namespace GastosApp.CognitoTriggers.CustomMessage;

// Sem ISender/Mediator/DI container aqui — diferente de AccountTriggerHandler
// (GastosApp.CognitoTriggers), este handler não orquestra nenhum caso de uso:
// só formata texto a partir do que o próprio evento do Cognito já traz (ver
// plan.md, decisão técnica 6).
public static class CustomMessageTriggerHandler
{
    private const string DefaultGreeting = "Olá";

    public static Task<CognitoCustomMessageEvent> HandleAsync(
        CognitoCustomMessageEvent evt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Assunto SEM "{{codigo}}"/"{####}" — confirmado ao vivo em hom
            // (validação da task 25) que o Cognito só substitui o placeholder
            // {####} em emailMessage, não em emailSubject; divergência da
            // decisão técnica 5 do plan.md, corrigida aqui.
            var (template, subject) = evt.TriggerSource switch
            {
                "CustomMessage_SignUp" or "CustomMessage_ResendCode"
                    => (EmailTemplateProvider.SignUpTemplate, "Confirme seu cadastro no jrn.expenses"),
                "CustomMessage_ForgotPassword"
                    => (EmailTemplateProvider.ForgotPasswordTemplate, "Redefinição de senha solicitada"),
                _ => ((string?)null, (string?)null) // fora do escopo — Cognito usa o texto padrão dele
            };

            if (template is not null)
            {
                var nome = evt.Request.UserAttributes.GetValueOrDefault("name");
                var email = evt.Request.UserAttributes.GetValueOrDefault("email", "");
                // Fallback defensivo (US4/Requisitos do spec.md) — não deixa
                // "{{nome}}" literal no e-mail se o atributo estiver ausente.
                var saudacao = string.IsNullOrWhiteSpace(nome) ? DefaultGreeting : nome;

                evt.Response.EmailMessage = Fill(template, evt.Request.CodeParameter, saudacao, email);
                evt.Response.EmailSubject = subject;
            }
        }
        catch (Exception ex)
        {
            // Nunca propaga: CustomMessage_* também é invocado de forma síncrona
            // dentro da própria chamada SignUpAsync/ResendConfirmationCodeAsync/
            // ForgotPasswordAsync (ver plan.md, decisão técnica 2) — uma falha
            // aqui devolve o evento sem alterar Response, e o Cognito cai no
            // texto padrão dele (US4 do spec.md). Só loga.
            logger.LogError(ex, "Falha ao formatar CustomMessage para TriggerSource {TriggerSource}.", evt.TriggerSource);
        }

        return Task.FromResult(evt);
    }

    // codigoParameter é o literal "{####}" — nunca o código real (ver
    // CognitoCustomMessageRequest.CodeParameter). O Cognito faz a substituição
    // de verdade depois que o Lambda retorna, só no corpo (emailMessage) —
    // confirmado ao vivo que emailSubject NÃO recebe essa substituição.
    private static string Fill(string texto, string codigoParameter, string nome, string email) =>
        texto
            .Replace("{{codigo}}", codigoParameter)
            .Replace("{{nome}}", nome)
            .Replace("{{email}}", email);
}
