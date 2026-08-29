namespace GastosApp.Domain.Categories;

public sealed class Category
{
    public string Id { get; }
    public string AccountId { get; }
    public string Nome { get; }
    public string Tipo { get; }
    public long? OrcamentoMensalCents { get; }
    public DateTimeOffset CreatedAt { get; }

    private Category(
        string id,
        string accountId,
        string nome,
        string tipo,
        long? orcamentoMensalCents,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        Nome = nome;
        Tipo = tipo;
        OrcamentoMensalCents = orcamentoMensalCents;
        CreatedAt = createdAt;
    }

    public static Category Create(string accountId, string nome, string tipo, long? orcamentoMensalCents)
    {
        return new Category(
            Guid.NewGuid().ToString(),
            accountId,
            nome,
            tipo,
            orcamentoMensalCents,
            DateTimeOffset.UtcNow);
    }

    public static Category Restore(
        string id,
        string accountId,
        string nome,
        string tipo,
        long? orcamentoMensalCents,
        DateTimeOffset createdAt)
    {
        return new Category(id, accountId, nome, tipo, orcamentoMensalCents, createdAt);
    }
}
