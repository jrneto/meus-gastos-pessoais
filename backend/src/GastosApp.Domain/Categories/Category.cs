namespace GastosApp.Domain.Categories;

public sealed class Category
{
    public string Id { get; }
    public string AccountId { get; }
    public string Nome { get; }
    public string Cor { get; }
    public string Icone { get; }
    public DateTimeOffset CreatedAt { get; }

    private Category(
        string id,
        string accountId,
        string nome,
        string cor,
        string icone,
        DateTimeOffset createdAt)
    {
        Id = id;
        AccountId = accountId;
        Nome = nome;
        Cor = cor;
        Icone = icone;
        CreatedAt = createdAt;
    }

    public static Category Create(string accountId, string nome, string cor, string icone)
    {
        return new Category(
            Guid.NewGuid().ToString(),
            accountId,
            nome,
            cor,
            icone,
            DateTimeOffset.UtcNow);
    }

    public static Category Restore(
        string id,
        string accountId,
        string nome,
        string cor,
        string icone,
        DateTimeOffset createdAt)
    {
        return new Category(id, accountId, nome, cor, icone, createdAt);
    }
}
