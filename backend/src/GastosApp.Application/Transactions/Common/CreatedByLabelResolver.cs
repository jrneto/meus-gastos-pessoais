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

        // Desde a FEAT-41, DELETE /members inativa (em vez de apagar) um membro
        // Ativo com transações lançadas — o Membership nunca mais desaparece de
        // fato nesse caso, só o Status muda pra Inativo. Como
        // FindByAccountAndUserIdAsync não filtra por Status, este resolver já
        // encontra o Membership Inativo normalmente e devolve o e-mail dele.
        // "Ex-membro" só dispara pra transações de um Membership que já foi
        // removido de fato antes da FEAT-41 existir (sem reconstituição
        // retroativa possível).
        return membership?.Email ?? "Ex-membro";
    }
}
