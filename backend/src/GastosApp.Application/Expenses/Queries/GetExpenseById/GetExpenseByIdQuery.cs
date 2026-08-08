using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using Mediator;

namespace GastosApp.Application.Expenses.Queries.GetExpenseById;

public sealed record GetExpenseByIdQuery(string UserId, string ExpenseId) : IQuery<Result<UpdateExpenseResult>>;

public sealed class GetExpenseByIdQueryHandler : IQueryHandler<GetExpenseByIdQuery, Result<UpdateExpenseResult>>
{
    private readonly IExpenseRepository _expenseRepository;

    public GetExpenseByIdQueryHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async ValueTask<Result<UpdateExpenseResult>> Handle(GetExpenseByIdQuery query, CancellationToken cancellationToken)
    {
        var expense = await _expenseRepository.GetByIdAsync(query.UserId, query.ExpenseId, cancellationToken);

        return expense is null
            ? Result.Failure<UpdateExpenseResult>(ExpenseErrors.NotFound)
            : Result.Success(UpdateExpenseResult.FromExpense(expense));
    }
}
