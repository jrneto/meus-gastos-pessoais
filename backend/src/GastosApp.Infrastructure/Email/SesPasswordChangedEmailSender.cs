using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Infrastructure.Email;

public sealed class SesPasswordChangedEmailSender : IPasswordChangedEmailSender
{
    // Igual ao <title> do template (03-senha-alterada.html).
    private const string Subject = "Sua senha foi alterada — jrn.expenses";

    private readonly IEmailSender _emailSender;

    public SesPasswordChangedEmailSender(IEmailSender emailSender) => _emailSender = emailSender;

    public Task SendAsync(string email, string? userAgent, CancellationToken cancellationToken = default)
    {
        var html = PasswordChangedEmailTemplateProvider.Template
            .Replace("{{email}}", email)
            .Replace("{{data}}", $"{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
            .Replace("{{dispositivo}}", string.IsNullOrWhiteSpace(userAgent) ? "Desconhecido" : userAgent);

        return _emailSender.SendAsync(email, Subject, html, cancellationToken);
    }
}
