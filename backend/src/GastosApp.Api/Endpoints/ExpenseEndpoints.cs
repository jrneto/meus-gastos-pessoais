using GastosApp.Api.Common;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using Mediator;
using System.Security.Claims;

namespace GastosApp.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses")
            .WithTags("Expenses")
            .RequireAuthorization();

        group.MapPost("/", RegisterExpense);

        return app;
    }

    private static async Task<IResult> RegisterExpense(
        RegisterExpenseRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var command = new RegisterExpenseCommand(
            userId!,
            request.Description,
            request.AmountInCents,
            request.Category,
            request.ExpenseDate);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created($"/expenses/{value.Id}", value));
    }
}

public record RegisterExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate);
