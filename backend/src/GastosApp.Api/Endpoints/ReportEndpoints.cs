using GastosApp.Api.Common;
using GastosApp.Application.Reports.Queries.GetReports;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reports")
            .WithTags("Reports")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Sem RoleEndpointFilters.Require — qualquer papel autenticado da conta
        // ativa pode consultar (spec.md, decisão de escopo 8 / US14).
        group.MapGet("/", GetReports)
            .Produces<GetReportsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetReports(
        [AsParameters] GetReportsRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetReportsQuery(currentAccount.AccountId!, request.Period, request.Date);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }
}

public record GetReportsRequest(string Period = "", string Date = "");
