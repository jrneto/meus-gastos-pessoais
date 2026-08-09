using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Logout;

public sealed record LogoutCommand : ICommand<Result>;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand, Result>
{
    public ValueTask<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        // Sem revogação server-side no Cognito nesta feature (fora do escopo,
        // ver spec.md) — a limpeza do cookie de refresh token acontece na
        // camada Api. Handler existe para seguir o padrão "rotas só chamam
        // o mediator" e já deixar o ponto de extensão pronto.
        return ValueTask.FromResult(Result.Success());
    }
}
