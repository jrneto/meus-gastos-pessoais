namespace GastosApp.Application.Common.Interfaces;

// FEAT-36: abstração genérica de envio de email — já pensada pra ser
// reaproveitada pela FEAT-37 (previsto desde a FEAT-33, que nomeia as duas
// como os consumidores do envio direto via SES).
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
