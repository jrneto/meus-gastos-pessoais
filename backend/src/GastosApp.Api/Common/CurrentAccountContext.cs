namespace GastosApp.Api.Common;

// Scoped por request — preenchido por ResolveAccountEndpointFilter,
// lido pelos endpoints de Category/Expense em vez de extrair o userId
// direto do JWT (o dado relevante pra essas rotas agora é o accountId).
public sealed class CurrentAccountContext
{
    public string? AccountId { get; set; }
}
