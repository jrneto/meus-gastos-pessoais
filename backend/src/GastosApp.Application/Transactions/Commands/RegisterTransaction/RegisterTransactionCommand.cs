using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Transactions;
using Mediator;

namespace GastosApp.Application.Transactions.Commands.RegisterTransaction;

public sealed record RegisterTransactionCommand(
    string AccountId,
    string Description,
    long AmountInCents,
    string CategoryId,
    string Tipo,
    DateOnly Date,
    string CreatedByUserId) : ICommand<Result<RegisterTransactionResult>>;

public sealed class RegisterTransactionCommandHandler : ICommandHandler<RegisterTransactionCommand, Result<RegisterTransactionResult>>
{
    private readonly ITransactionRepository _transactionRepository;

    public RegisterTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async ValueTask<Result<RegisterTransactionResult>> Handle(RegisterTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Create(
            command.AccountId,
            command.Description,
            command.AmountInCents,
            command.CategoryId,
            command.Tipo,
            command.Date,
            command.CreatedByUserId);

        await _transactionRepository.SaveAsync(transaction, cancellationToken);

        // Quem cria é sempre o próprio chamador — "Você" sem precisar consultar
        // Membership aqui (diferente de GetTransactions/GetTransactionById, que
        // podem mostrar autoria de outro membro).
        return Result.Success(RegisterTransactionResult.FromEntity(transaction, createdByLabel: "Você"));
    }
}

public sealed record RegisterTransactionResult(
    string Id,
    string Description,
    long AmountInCents,
    string CategoryId,
    string Tipo,
    DateOnly Date,
    string CreatedByUserId,
    string CreatedByLabel,
    DateTimeOffset CreatedAt)
{
    public static RegisterTransactionResult FromEntity(Transaction transaction, string createdByLabel) => new(
        transaction.Id,
        transaction.Description,
        transaction.AmountInCents,
        transaction.CategoryId,
        transaction.Tipo,
        transaction.Date,
        transaction.CreatedByUserId,
        createdByLabel,
        transaction.CreatedAt);
}
