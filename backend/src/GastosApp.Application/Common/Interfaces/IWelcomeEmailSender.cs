namespace GastosApp.Application.Common.Interfaces;

// FEAT-37: monta e envia o email de boas-vindas — específico deste
// fluxo, composto sobre IEmailSender + IUserProfileRepository (pra
// resolver o nome real do usuário). Mesma forma de
// IPasswordChangedEmailSender (FEAT-36): a Application só entrega
// userId/email, quem sabe montar o HTML é a Infrastructure.
public interface IWelcomeEmailSender
{
    Task SendAsync(string userId, string email, CancellationToken cancellationToken = default);
}
