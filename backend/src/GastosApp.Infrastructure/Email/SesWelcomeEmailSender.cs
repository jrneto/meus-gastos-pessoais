using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Infrastructure.Email;

public sealed class SesWelcomeEmailSender : IWelcomeEmailSender
{
    // Igual ao <title> do template (04-boas-vindas.html).
    private const string Subject = "Bem-vindo ao jrn.expenses";

    private readonly IEmailSender _emailSender;
    private readonly IUserProfileRepository _profileRepository;

    public SesWelcomeEmailSender(IEmailSender emailSender, IUserProfileRepository profileRepository)
    {
        _emailSender = emailSender;
        _profileRepository = profileRepository;
    }

    public async Task SendAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        // Sem fallback textual (decisão do usuário, ver spec.md decisão 2):
        // um usuário confirmado sem UserProfile é uma anomalia (a FEAT-31 já
        // bloqueia esse mesmo usuário no login) — perfil ausente é só mais
        // uma causa de falha no envio, capturada pelo try/catch já existente
        // em EnsureAccountCommandHandler, não um caminho de conteúdo
        // alternativo.
        var profile = await _profileRepository.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"UserProfile não encontrado para o usuário {userId} — email de boas-vindas não enviado.");

        var html = WelcomeEmailTemplateProvider.Template
            .Replace("{{nome}}", profile.Name)
            .Replace("{{email}}", email);

        await _emailSender.SendAsync(email, Subject, html, cancellationToken);
    }
}
