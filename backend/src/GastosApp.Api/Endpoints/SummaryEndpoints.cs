using GastosApp.Api.Common;
using GastosApp.Application.Summary.Queries.GetSummary;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class SummaryEndpoints
{
    public static IEndpointRouteBuilder MapSummaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/summary")
            .WithTags("Summary")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sem RoleEndpointFilters.Require — qualquer papel autenticado da conta
        // ativa pode consultar (spec.md, decisão de escopo 7 / US10).
        group.MapGet("/", GetSummary)
            .Produces<GetSummaryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSummary(
        [AsParameters] GetSummaryRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetSummaryQuery(currentAccount.AccountId!, currentAccount.UserId!, request.Month);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}

public record GetSummaryRequest(string Month = "");
