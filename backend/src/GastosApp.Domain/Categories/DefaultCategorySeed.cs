namespace GastosApp.Domain.Categories;

/// <summary>
/// Catálogo fixo das 13 categorias padrão criadas automaticamente para toda
/// <c>Account</c> nova (FEAT-28). Dado de negócio, não configuração de
/// ambiente — os ids são literais hardcoded, iguais em todo ambiente
/// (dev/hom/prod), para ficar fácil rastrear a mesma categoria entre eles.
/// Ver backend/specs/FEAT-28-seed-categorias-padrao/spec.md.
/// </summary>
public static class DefaultCategorySeed
{
    public const string Tipo = "despesa";

    public static readonly IReadOnlyList<(string Id, string Nome)> Items =
    [
        ("862d8a7c-c3ef-412b-b4d3-88c1b4d317d9", "Moradia"),
        ("369a308a-f96e-4ba9-ac43-3c9e8696141f", "Alimentação"),
        ("a95ac718-1608-4c64-96da-4eefdc33e3e9", "Transporte"),
        ("2644f155-1215-4936-8f9a-606e0ba58315", "Saúde"),
        ("ceb83cec-9ca0-4ec0-a58f-adac83574faf", "Educação"),
        ("f2d554c0-16d6-4fee-bef1-3364d9bb8ec3", "Filhos e Dependentes"),
        ("24ef9ebc-58b3-4197-b9ac-1f203b79f07b", "Lazer e Entretenimento"),
        ("0af4581d-37bf-4636-9805-ce2302403330", "Vestuário e Cuidados Pessoais"),
        ("319ddec7-f867-427f-997a-66cd4ed9d8e1", "Pets"),
        ("89bfe4ec-8747-44d3-92ba-4266960dd00f", "Dívidas e Financiamentos"),
        ("961a8b3c-d210-4bd5-a470-1ef15c3549c3", "Impostos, Taxas e Seguros"),
        ("d8865733-b002-4b11-b160-94237b2391c1", "Doações e Presentes"),
        ("e9b32f2d-3eb7-4318-a268-438bb2d72f44", "Outros"),
    ];
}
