using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Members;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Application.Transactions.Commands.DeleteTransaction;

public sealed record DeleteTransactionCommand(
    string AccountId,
    string TransactionId,
    string CallerUserId,
    MembershipRole CallerRole) : ICommand<Result>;

public sealed class DeleteTransactionCommandHandler : ICommandHandler<DeleteTransactionCommand, Result>
{
    private readonly ITransactionRepository _transactionRepository;

    public DeleteTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async ValueTask<Result> Handle(DeleteTransactionCommand command, CancellationToken cancellationToken)
    {
        var existing = await _transactionRepository.GetByIdAsync(command.AccountId, command.TransactionId, cancellationToken);
        if (existing is null)
            return Result.Failure(TransactionErrors.NotFound);

        if (command.CallerRole == MembershipRole.Lancar && existing.CreatedByUserId != command.CallerUserId)
            return Result.Failure(MembershipErrors.InsufficientPermission);

        var deleted = await _transactionRepository.DeleteAsync(command.AccountId, command.TransactionId, cancellationToken);
        return deleted ? Result.Success() : Result.Failure(TransactionErrors.NotFound);
    }
}
