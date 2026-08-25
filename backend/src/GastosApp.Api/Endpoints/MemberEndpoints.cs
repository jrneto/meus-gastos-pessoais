using GastosApp.Api.Common;
using GastosApp.Application.Members;
using GastosApp.Application.Members.Commands.InviteMember;
using GastosApp.Application.Members.Commands.RemoveMember;
using GastosApp.Application.Members.Commands.UpdateMemberRole;
using GastosApp.Application.Members.Queries.GetMembers;
using GastosApp.Domain.Accounts;
using Mediator;

namespace GastosApp.Api.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/members")
            .WithTags("Members")
            .RequireAuthorization()
            .AddEndpointFilter<ResolveAccountEndpointFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/", GetMembers)
            .Produces<GetMembersResult>(StatusCodes.Status200OK);

        group.MapPost("/", InviteMember)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
            .Produces<MemberResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id}", UpdateMemberRole)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
            .Produces<MemberResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}", RemoveMember)
            .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> GetMembers(
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetMembersQuery(currentAccount.AccountId!);

        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> InviteMember(
        InviteMemberRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new InviteMemberCommand(currentAccount.AccountId!, request.Email, request.Role);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created($"/members/{value.Id}", value));
    }

    private static async Task<IResult> UpdateMemberRole(
        string id,
        UpdateMemberRoleRequest request,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMemberRoleCommand(currentAccount.AccountId!, id, request.Role);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> RemoveMember(
        string id,
        CurrentAccountContext currentAccount,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RemoveMemberCommand(currentAccount.AccountId!, id);

        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(() => Results.NoContent());
    }
}

public record InviteMemberRequest(string Email, string Role);

public record UpdateMemberRoleRequest(string Role);
