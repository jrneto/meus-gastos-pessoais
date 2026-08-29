using System.Globalization;
using Amazon.DynamoDBv2.Model;
using GastosApp.Domain.Categories;

namespace GastosApp.Infrastructure.Categories;

// Fonte única de verdade do formato do item Category no DynamoDB. Extraído de
// dentro de DynamoDbCategoryRepository (FEAT-28) porque DynamoDbAccountRepository
// passou a escrever esse mesmo formato de item, atomicamente, ao semear as
// categorias padrão na criação da conta — sem essa extração haveria duas
// fontes de verdade divergentes pro shape do item Category.
internal static class CategoryItemMapper
{
    public const string SkPrefix = "CAT#";
    public const string TipoAttribute = "Tipo";
    public const string TipoCategoria = "categoria";

    public static string BuildSk(string nome) => $"{SkPrefix}{CategorySlug.From(nome)}";

    public static Dictionary<string, AttributeValue> BuildItem(Category category, string sk)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{category.AccountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{category.Id}" },
            ["Nome"] = new AttributeValue { S = category.Nome },
            [TipoAttribute] = new AttributeValue { S = TipoCategoria },
            ["TipoLancamento"] = new AttributeValue { S = category.Tipo },
            ["CreatedAt"] = new AttributeValue { S = category.CreatedAt.ToString("O") }
        };

        if (category.OrcamentoMensalCents is { } orcamento)
            item["OrcamentoMensalCents"] = new AttributeValue { N = orcamento.ToString(CultureInfo.InvariantCulture) };

        return item;
    }
}
