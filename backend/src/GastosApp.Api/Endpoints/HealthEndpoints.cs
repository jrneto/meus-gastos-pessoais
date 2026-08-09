using GastosApp.Api.Common;
using GastosApp.Application.Health;
using GastosApp.Application.Health.Queries.GetHealth;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth)
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }

    // Sem RequireAuthorization() de propósito — rastreabilidade de versão
    // (ver backend/specs/FEAT-14-cicd-github-actions/spec.md, US3) precisa
    // ser consultável externamente, sem exigir login.
    private static async Task<IResult> GetHealth(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetHealthQuery(), cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }
}
