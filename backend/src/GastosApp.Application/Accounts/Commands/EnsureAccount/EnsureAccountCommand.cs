using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Accounts.Commands.EnsureAccount;

// Cria a Account+Membership (Titular) do usuário se ainda não existir,
// ou só resolve a já existente. Idempotente sob concorrência — a
// resolução real da corrida acontece dentro de
// IAccountRepository.CreateAsync (ver DynamoDbAccountRepository).
// Despachado tanto pelo trigger PostConfirmation do Cognito
// (GastosApp.CognitoTriggers) quanto pelo fallback do login
// (LoginUserCommandHandler) — nunca pelas rotas de Category/Expense,
// que só resolvem via ResolveMembershipQuery.
// Desde a FEAT-28, quando a Account é criada (AlreadyExisted: false),
// IAccountRepository.CreateAsync também semeia atomicamente as 13
// categorias padrão (DefaultCategorySeed) na mesma transação — este
// Command/Handler não precisa saber disso, é transparente pra quem chama.
public sealed record EnsureAccountCommand(string UserId, string Email) : ICommand<Result<EnsureAccountResult>>;

public sealed record EnsureAccountResult(string AccountId, bool AlreadyExisted);

public sealed class EnsureAccountCommandHandler : ICommandHandler<EnsureAccountCommand, Result<EnsureAccountResult>>
{
    private readonly IAccountRepository _accountRepository;

    public EnsureAccountCommandHandler(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async ValueTask<Result<EnsureAccountResult>> Handle(EnsureAccountCommand command, CancellationToken cancellationToken)
    {
        var existingAccountId = await _accountRepository.FindAccountIdByUserIdAsync(command.UserId, cancellationToken);
        if (existingAccountId is not null)
            return Result.Success(new EnsureAccountResult(existingAccountId, AlreadyExisted: true));

        var created = await _accountRepository.CreateAsync(command.UserId, command.Email, cancellationToken);
        return Result.Success(new EnsureAccountResult(created.AccountId, created.AlreadyExisted));
    }
}
