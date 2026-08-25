using System.Text.Json.Serialization;
using GastosApp.Application.Accounts.Commands.EnsureAccount;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Members.Commands.AcceptPendingInvites;
using Mediator;
using Microsoft.Extensions.Logging;

namespace GastosApp.Application.Auth.Commands.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<Result<LoginUserResult>>;

public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<LoginUserResult>>
{
    private readonly IAuthService _authService;
    private readonly ISender _sender;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(IAuthService authService, ISender sender, ILogger<LoginUserCommandHandler> logger)
    {
        _authService = authService;
        _sender = sender;
        _logger = logger;
    }

    public async ValueTask<Result<LoginUserResult>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return Result.Failure<LoginUserResult>(AuthErrors.Validation("Email é obrigatório."));
        if (string.IsNullOrWhiteSpace(command.Password))
            return Result.Failure<LoginUserResult>(AuthErrors.Validation("Senha é obrigatória."));

        var result = await _authService.LoginAsync(command.Email, command.Password, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<LoginUserResult>(result.Error!);

        // Fallback de criação de conta (FEAT-19): normalmente a Account já existe,
        // criada pelo trigger PostConfirmation do Cognito. Se não existir ainda
        // (trigger falhou, usuário criado fora do fluxo padrão, ambiente local sem
        // o trigger), EnsureAccountCommand cria agora, de forma idempotente. Falha
        // aqui nunca pode derrubar o login (efeito colateral, não fluxo de negócio
        // — ver plan.md, decisão técnica 2) — só loga.
        try
        {
            await _sender.Send(new EnsureAccountCommand(result.Value.UserId, command.Email), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao garantir Account para o usuário {UserId} no login.", result.Value.UserId);
        }

        // Aceitação de convites pendentes (FEAT-20): melhor esforço, sempre depois
        // de garantir a conta própria — nunca derruba o login (mesmo espírito do
        // bloco acima). Pode trocar a conta ativa do usuário.
        try
        {
            await _sender.Send(new AcceptPendingInvitesCommand(result.Value.UserId, command.Email), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao aceitar convites pendentes para o usuário {UserId} no login.", result.Value.UserId);
        }

        return Result.Success(LoginUserResult.FromLoginResult(result.Value));
    }
}

public record LoginUserResult(string AccessToken, int ExpiresIn, string UserId, [property: JsonIgnore] string RefreshToken)
{
    public static LoginUserResult FromLoginResult(LoginResult result) =>
        new(result.AccessToken, result.ExpiresIn, result.UserId, result.RefreshToken);
}
