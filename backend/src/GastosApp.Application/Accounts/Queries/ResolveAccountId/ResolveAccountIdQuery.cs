using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Accounts.Queries.ResolveAccountId;

// Só lê — nunca cria (ao contrário de EnsureAccountCommand). Usado pelo
// ResolveAccountEndpointFilter da Api pra resolver o accountId de toda
// rota autenticada de Category/Expense. Ausência de conta aqui é 401,
// nunca auto-cura (situação que só ocorreria por dado corrompido/manual
// — em uso normal a conta já existe antes do primeiro request
// autenticado, criada pelo trigger do Cognito ou pelo fallback do login).
public sealed record ResolveAccountIdQuery(string UserId) : IQuery<Result<string>>;

public sealed class ResolveAccountIdQueryHandler : IQueryHandler<ResolveAccountIdQuery, Result<string>>
{
    private readonly IAccountRepository _accountRepository;

    public ResolveAccountIdQueryHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async ValueTask<Result<string>> Handle(ResolveAccountIdQuery query, CancellationToken cancellationToken)
    {
        var accountId = await _accountRepository.FindAccountIdByUserIdAsync(query.UserId, cancellationToken);

        return accountId is null
            ? Result.Failure<string>(AccountErrors.NotResolved)
            : Result.Success(accountId);
    }
}
