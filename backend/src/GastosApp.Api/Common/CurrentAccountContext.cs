using GastosApp.Domain.Accounts;

namespace GastosApp.Api.Common;

// Scoped por request — preenchido por ResolveAccountEndpointFilter,
// lido pelos endpoints de Category/Expense/Members em vez de extrair o
// userId direto do JWT (o dado relevante pra essas rotas agora é o
// accountId) e por RoleEndpointFilters pra decidir autorização por papel.
public sealed class CurrentAccountContext
{
    public string? AccountId { get; set; }
    public string? MembershipId { get; set; }
    public MembershipRole? Role { get; set; }
    public string? UserId { get; set; }
}
