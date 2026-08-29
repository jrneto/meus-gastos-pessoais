using GastosApp.Domain.Accounts;

namespace GastosApp.Application.Members;

public sealed record MemberResult(string Id, string Email, string Role, string Status, DateTimeOffset CreatedAt)
{
    // Role/Status viram string via ToString() — os nomes dos membros dos enums
    // (Titular/Leitura/Lancar/Total, Ativo/ConvitePendente) já batem 1:1 com o
    // contrato da spec, sem precisar de JsonStringEnumConverter (projeto não
    // expõe enum nenhum via JSON, ver Expense.CategoryId/FEAT-17).
    public static MemberResult FromEntity(Membership membership) => new(
        membership.Id,
        membership.Email,
        membership.Role.ToString(),
        membership.Status.ToString(),
        membership.CreatedAt);
}

public sealed record GetMembersResult(IReadOnlyList<MemberResult> Items)
{
    public static GetMembersResult FromEntities(IReadOnlyList<Membership> memberships) =>
        new(memberships.OrderBy(m => m.CreatedAt).Select(MemberResult.FromEntity).ToList());
}
