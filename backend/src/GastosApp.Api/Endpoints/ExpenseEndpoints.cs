using GastosApp.Api.Common;
using GastosApp.Application.Expenses.Commands.DeleteExpense;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Application.Expenses.Queries.GetExpenses;
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
        group.MapGet("/", GetExpenses);
        group.MapPut("/{id}", UpdateExpense);
        group.MapDelete("/{id}", DeleteExpense);

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

    private static async Task<IResult> GetExpenses(
        [AsParameters] GetExpensesRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var query = new GetExpensesQuery(
            userId!,
            request.YearMonth,
            request.Category,
            request.DateFrom,
            request.DateTo,
            request.MinAmountInCents,
            request.MaxAmountInCents,
            request.Cursor,
            request.Limit);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> UpdateExpense(
        string id,
        UpdateExpenseRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var command = new UpdateExpenseCommand(
            userId!,
            id,
            request.Description,
            request.AmountInCents,
            request.Category,
            request.ExpenseDate);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> DeleteExpense(
        string id,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var command = new DeleteExpenseCommand(userId!, id);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(() => Results.NoContent());
    }
}

public record RegisterExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate);

public record UpdateExpenseRequest(string Description, long AmountInCents, string Category, DateOnly ExpenseDate);

public record GetExpensesRequest(
    string? YearMonth,
    string? Category,
    string? DateFrom,
    string? DateTo,
    long? MinAmountInCents,
    long? MaxAmountInCents,
    string? Cursor,
    int? Limit);
