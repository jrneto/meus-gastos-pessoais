using GastosApp.Application.Common.Results;
using GastosApp.Application.Members;
using GastosApp.Domain.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace GastosApp.Api.Common;

// Filtros de autorização por papel, um delegate factory por combinação de
// papéis permitidos — Minimal API não parametriza bem IEndpointFilter
// registrado via AddEndpointFilter<T>() com argumentos diferentes por rota
// (ver plan.md, decisão técnica 5). Roda sempre depois de
// ResolveAccountEndpointFilter (que já populou CurrentAccountContext.Role).
public static class RoleEndpointFilters
{
    public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> Require(
        params MembershipRole[] allowedRoles)
    {
        return async (context, next) =>
        {
            var currentAccount = context.HttpContext.RequestServices.GetRequiredService<CurrentAccountContext>();
            if (currentAccount.Role is null || !allowedRoles.Contains(currentAccount.Role.Value))
                return Result.Failure(MembershipErrors.InsufficientPermission).ToHttpResult(() => Results.Ok());

            return await next(context);
        };
    }
}
