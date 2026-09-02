namespace GastosApp.Application.Common.Interfaces;

// FEAT-36: monta e envia o email de aviso "senha alterada" — específico
// deste fluxo, composto sobre IEmailSender.
public interface IPasswordChangedEmailSender
{
    Task SendAsync(string email, string? userAgent, CancellationToken cancellationToken = default);
}
