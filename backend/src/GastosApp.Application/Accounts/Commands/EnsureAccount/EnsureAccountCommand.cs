using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnsureAccountCommandHandler> _logger;

    // IWelcomeEmailSender é resolvido tardiamente (via IServiceProvider, não
    // injeção direta no construtor) de propósito: o Mediator constrói todo o
    // handler — incluindo a cadeia de dependências de IWelcomeEmailSender —
    // ANTES de Handle() rodar. Com injeção direta, uma falha na CONSTRUÇÃO
    // dessa cadeia (não só na chamada) escaparia do try/catch abaixo e
    // abortaria EnsureAccountCommand inteiro — exatamente o bug real
    // encontrado em produção na FEAT-37 (SES client falhando ao resolver
    // região, ArgumentNullException na resolução de DI, conta nunca criada).
    // Resolver dentro do try/catch garante que QUALQUER falha relacionada ao
    // email — construção ou envio — nunca bloqueia a criação da conta.
    public EnsureAccountCommandHandler(
        IAccountRepository accountRepository,
        IServiceProvider serviceProvider,
        ILogger<EnsureAccountCommandHandler> logger)
    {
        _accountRepository = accountRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask<Result<EnsureAccountResult>> Handle(EnsureAccountCommand command, CancellationToken cancellationToken)
    {
        var existingAccountId = await _accountRepository.FindAccountIdByUserIdAsync(command.UserId, cancellationToken);
        if (existingAccountId is not null)
            return Result.Success(new EnsureAccountResult(existingAccountId, AlreadyExisted: true));

        var created = await _accountRepository.CreateAsync(command.UserId, command.Email, cancellationToken);

        if (!created.AlreadyExisted)
        {
            try
            {
                var welcomeEmailSender = _serviceProvider.GetRequiredService<IWelcomeEmailSender>();
                await welcomeEmailSender.SendAsync(command.UserId, command.Email, cancellationToken);
            }
            catch (Exception ex)
            {
                // Nunca propaga: a conta já foi criada de fato (FEAT-37,
                // spec.md) — falha no envio deste email de boas-vindas não
                // pode derrubar EnsureAccountCommand. Mesma filosofia
                // defensiva do ResetPasswordCommandHandler (FEAT-36).
                _logger.LogError(ex, "Falha ao enviar email de boas-vindas para o usuário {UserId}.", command.UserId);
            }
        }

        return Result.Success(new EnsureAccountResult(created.AccountId, created.AlreadyExisted));
    }
}
