using GastosApp.Api.Common;
using GastosApp.Application.Transactions.Commands.DeleteTransaction;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Application.Transactions.Queries.GetTransactionById;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions")
            .WithTags("Transactions")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/", RegisterTransaction)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
            .Produces<RegisterTransactionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", GetTransactions)
            .Produces<GetTransactionsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", GetTransactionById)
            .Produces<UpdateTransactionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Lancar passa no gate estático — posse (createdByUserId == chamador) é
        // checada dentro do Handler (Update/DeleteTransactionCommandHandler).
        group.MapPut("/{id}", UpdateTransaction)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
            .Produces<UpdateTransactionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id}", DeleteTransaction)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RegisterTransaction(
        RegisterTransactionRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RegisterTransactionCommand(
            currentAccount.AccountId!,
            request.Description,
            request.AmountInCents,
            request.CategoryId,
            request.Tipo,
            request.Date,
            currentAccount.UserId!);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created($"/transactions/{value.Id}", value));
    }

    private static async Task<IResult> GetTransactions(
        [AsParameters] GetTransactionsRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetTransactionsQuery(
            currentAccount.AccountId!,
            currentAccount.UserId!,
            NullIfEmpty(request.Tipo),
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

    private static async Task<IResult> GetTransactionById(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetTransactionByIdQuery(currentAccount.AccountId!, id, currentAccount.UserId!);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> UpdateTransaction(
        string id,
        UpdateTransactionRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTransactionCommand(
            currentAccount.AccountId!,
            id,
            currentAccount.UserId!,
            currentAccount.Role!.Value,
            request.Description,
            request.AmountInCents,
            request.CategoryId,
            request.Tipo,
            request.Date);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> DeleteTransaction(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeleteTransactionCommand(currentAccount.AccountId!, id, currentAccount.UserId!, currentAccount.Role!.Value);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(() => Results.NoContent());
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

public record RegisterTransactionRequest(string Description, long AmountInCents, string CategoryId, string Tipo, DateOnly Date);

public record UpdateTransactionRequest(string Description, long AmountInCents, string CategoryId, string Tipo, DateOnly Date);

public record GetTransactionsRequest(
    string Tipo = "",
    string YearMonth = "",
    string CategoryId = "",
    string DateFrom = "",
    string DateTo = "",
    long? MinAmountInCents = null,
    long? MaxAmountInCents = null,
    string Cursor = "",
    int? Limit = null);
