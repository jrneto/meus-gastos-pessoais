using GastosApp.Api.Common;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Categories.Commands.DeleteCategory;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Categories.Queries.GetCategories;
using GastosApp.Application.Categories.Queries.GetCategoryById;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories")
            .WithTags("Categories")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", GetCategories)
            .Produces<GetCategoriesResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", GetCategoryById)
            .Produces<UpdateCategoryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateCategory)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total, MembershipRole.Titular))
            .Produces<CreateCategoryResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id}", UpdateCategory)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total, MembershipRole.Titular))
            .Produces<UpdateCategoryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}", DeleteCategory)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total, MembershipRole.Titular))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> GetCategories(
        [AsParameters] GetCategoriesRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetCategoriesQuery(currentAccount.AccountId!, NullIfEmpty(request.Tipo));

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetCategoryById(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetCategoryByIdQuery(currentAccount.AccountId!, id);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> CreateCategory(
        CreateCategoryRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateCategoryCommand(
            currentAccount.AccountId!, request.Nome, request.Tipo, request.OrcamentoMensalCents);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created($"/categories/{value.Id}", value));
    }

    private static async Task<IResult> UpdateCategory(
        string id,
        UpdateCategoryRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCategoryCommand(
            currentAccount.AccountId!, id, request.Nome, request.Tipo, request.OrcamentoMensalCents);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> DeleteCategory(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCategoryCommand(currentAccount.AccountId!, id);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(() => Results.NoContent());
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}

public record CreateCategoryRequest(string Nome, string Tipo, long? OrcamentoMensalCents);

public record UpdateCategoryRequest(string Nome, string Tipo, long? OrcamentoMensalCents);

public record GetCategoriesRequest(string Tipo = "");
