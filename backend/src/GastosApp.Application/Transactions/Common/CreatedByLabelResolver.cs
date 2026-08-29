using GastosApp.Application.Common.Interfaces;

namespace GastosApp.Application.Transactions.Common;

// Reaproveitado por Update/DeleteTransactionCommandHandler e por
// GetTransactions(ById)QueryHandler pra transformar createdByUserId num rótulo
// exibível — "Você" quando o autor é o próprio chamador, senão o e-mail do
// Membership dele.
internal static class CreatedByLabelResolver
{
    public static async Task<string> ResolveAsync(
        IMembershipRepository membershipRepository,
        string accountId,
        string createdByUserId,
        string callerUserId,
        CancellationToken cancellationToken)
    {
        if (createdByUserId == callerUserId)
            return "Você";

        var membership = await membershipRepository.FindByAccountAndUserIdAsync(accountId, createdByUserId, cancellationToken);

        // Hoje (FEAT-20) DELETE /members ainda apaga o Membership de fato, mesmo
        // que o membro tenha transações lançadas — "Ex-membro" cobre esse caso.
        // Confirmado com o usuário como débito técnico (ver backend/docs/backlog.md):
        // um membro com transações deveria virar Inativo em vez de removido; quando
        // isso for implementado, o Membership nunca mais desaparece de fato (só o
        // Status muda), então este fallback deixa de disparar — sem exigir nenhuma
        // mudança neste resolver, já que FindByAccountAndUserIdAsync não filtra por
        // Status hoje.
        return membership?.Email ?? "Ex-membro";
    }
}
