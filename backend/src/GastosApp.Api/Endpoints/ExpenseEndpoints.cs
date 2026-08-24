using GastosApp.Api.Common;
using GastosApp.Application.Expenses.Commands.DeleteExpense;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Application.Expenses.Queries.GetExpenseById;
using GastosApp.Application.Expenses.Queries.GetExpenses;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses")
            .WithTags("Expenses")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/", RegisterExpense)
            .Produces<RegisterExpenseResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetExpenses)
            .Produces<GetExpensesResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", GetExpenseById)
            .Produces<UpdateExpenseResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id}", UpdateExpense)
            .Produces<UpdateExpenseResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id}", DeleteExpense)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterExpense(
        RegisterExpenseRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RegisterExpenseCommand(
            currentAccount.AccountId!,
            request.Description,
            request.AmountInCents,
            request.CategoryId,
            request.ExpenseDate);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created($"/expenses/{value.Id}", value));
    }

    private static async Task<IResult> GetExpenses(
        [AsParameters] GetExpensesRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetExpensesQuery(
            currentAccount.AccountId!,
            NullIfEmpty(request.YearMonth),
            NullIfEmpty(request.CategoryId),
            NullIfEmpty(request.DateFrom),
            NullIfEmpty(request.DateTo),
            request.MinAmountInCents,
            request.MaxAmountInCents,
            NullIfEmpty(request.Cursor),
            request.Limit);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetExpenseById(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetExpenseByIdQuery(currentAccount.AccountId!, id);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> UpdateExpense(
        string id,
        UpdateExpenseRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateExpenseCommand(
            currentAccount.AccountId!,
            id,
            request.Description,
            request.AmountInCents,
            request.CategoryId,
            request.ExpenseDate);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> DeleteExpense(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeleteExpenseCommand(currentAccount.AccountId!, id);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(() => Results.NoContent());
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

public record RegisterExpenseRequest(string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate);

public record UpdateExpenseRequest(string Description, long AmountInCents, string CategoryId, DateOnly ExpenseDate);

public record GetExpensesRequest(
    string YearMonth = "",
    string CategoryId = "",
    string DateFrom = "",
    string DateTo = "",
    long? MinAmountInCents = null,
    long? MaxAmountInCents = null,
    string Cursor = "",
    int? Limit = null);
