using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Application.Transactions.Common;
using Mediator;

namespace GastosApp.Application.Transactions.Queries.GetTransactionById;

public sealed record GetTransactionByIdQuery(string AccountId, string TransactionId, string CallerUserId)
    : IQuery<Result<UpdateTransactionResult>>;

public sealed class GetTransactionByIdQueryHandler : IQueryHandler<GetTransactionByIdQuery, Result<UpdateTransactionResult>>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMembershipRepository _membershipRepository;

    public GetTransactionByIdQueryHandler(ITransactionRepository transactionRepository, IMembershipRepository membershipRepository)
    {
        _transactionRepository = transactionRepository;
        _membershipRepository = membershipRepository;
    }

    public async ValueTask<Result<UpdateTransactionResult>> Handle(GetTransactionByIdQuery query, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(query.AccountId, query.TransactionId, cancellationToken);
        if (transaction is null)
            return Result.Failure<UpdateTransactionResult>(TransactionErrors.NotFound);

        var createdByLabel = await CreatedByLabelResolver.ResolveAsync(
            _membershipRepository, query.AccountId, transaction.CreatedByUserId, query.CallerUserId, cancellationToken);

        return Result.Success(UpdateTransactionResult.FromEntity(transaction, createdByLabel));
    }
}
