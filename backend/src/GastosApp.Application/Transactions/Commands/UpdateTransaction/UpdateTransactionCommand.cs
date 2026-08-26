using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Members;
using GastosApp.Application.Transactions.Common;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Transactions;
using Mediator;

namespace GastosApp.Application.Transactions.Commands.UpdateTransaction;

public sealed record UpdateTransactionCommand(
    string AccountId,
    string TransactionId,
    string CallerUserId,
    MembershipRole CallerRole,
    string Description,
    long AmountInCents,
    string CategoryId,
    string Tipo,
    DateOnly Date) : ICommand<Result<UpdateTransactionResult>>;

public sealed class UpdateTransactionCommandHandler : ICommandHandler<UpdateTransactionCommand, Result<UpdateTransactionResult>>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMembershipRepository _membershipRepository;

    public UpdateTransactionCommandHandler(ITransactionRepository transactionRepository, IMembershipRepository membershipRepository)
    {
        _transactionRepository = transactionRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<UpdateTransactionResult>> Handle(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        var existing = await _transactionRepository.GetByIdAsync(command.AccountId, command.TransactionId, cancellationToken);
        if (existing is null)
            return Result.Failure<UpdateTransactionResult>(TransactionErrors.NotFound);

        // Total/Titular editam qualquer transação da conta; Lancar só a que ele
        // mesmo criou (RoleEndpointFilters já barrou Leitura antes de chegar aqui).
        if (command.CallerRole == MembershipRole.Lancar && existing.CreatedByUserId != command.CallerUserId)
            return Result.Failure<UpdateTransactionResult>(MembershipErrors.InsufficientPermission);

        var updated = await _transactionRepository.UpdateAsync(
            command.AccountId,
            command.TransactionId,
            command.Description,
            command.AmountInCents,
            command.CategoryId,
            command.Tipo,
            command.Date,
            cancellationToken);

        if (updated is null) // defensivo: GetByIdAsync acima já confirmou existência
            return Result.Failure<UpdateTransactionResult>(TransactionErrors.NotFound);

        var createdByLabel = await CreatedByLabelResolver.ResolveAsync(
            _membershipRepository, command.AccountId, updated.CreatedByUserId, command.CallerUserId, cancellationToken);

        return Result.Success(UpdateTransactionResult.FromEntity(updated, createdByLabel));
    }
}

public sealed record UpdateTransactionResult(
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
    public static UpdateTransactionResult FromEntity(Transaction transaction, string createdByLabel) => new(
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
