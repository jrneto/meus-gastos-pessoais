using System.Security.Claims;
using GastosApp.Application.Members.Queries.ResolveMembership;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace GastosApp.Api.Common;

// Aplicado nos grupos /categories, /expenses e /members (depois de
// .RequireAuthorization()). Resolve o accountId + papel do userId
// autenticado uma única vez por request, evitando duplicar essa chamada
// em cada endpoint — e garante que ausência de conta/membership vira
// sempre 401 (account-not-found), nunca um erro esquecido em algum
// handler.
public sealed class ResolveAccountEndpointFilter : IEndpointFilter
{
    private readonly ISender _sender;
    private readonly CurrentAccountContext _currentAccount;

    public ResolveAccountEndpointFilter(ISender sender, CurrentAccountContext currentAccount)
    {
        _sender = sender;
        _currentAccount = currentAccount;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Defensivo — RequireAuthorization() já garante userId presente antes
        // deste filtro rodar, mas evita NullReferenceException se isso mudar.
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Json(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Type = "https://gastosapp.dev/errors/unauthorized"
            }, AppJsonSerializerContext.Default.ProblemDetails, statusCode: StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        }

        var result = await _sender.Send(new ResolveMembershipQuery(userId), context.HttpContext.RequestAborted);
        if (result.IsFailure)
            return result.ToHttpResult(_ => Results.Ok());

        _currentAccount.AccountId = result.Value.AccountId;
        _currentAccount.MembershipId = result.Value.MembershipId;
        _currentAccount.Role = result.Value.Role;
        _currentAccount.UserId = userId;
        return await next(context);
    }
}
