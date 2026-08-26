using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Categories;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Categories;

public sealed class DynamoDbCategoryRepository : ICategoryRepository
{
    private const string SkPrefix = "CAT#";
    private const string Gsi2Index = "GSI2";

    // GSI2 (GSI2PK = "ID#{id}") é compartilhado com Expense (mesmo formato de
    // chave) — sem esse discriminador, um id de despesa passado por engano a
    // um endpoint de categoria encontraria o item errado: GetByIdAsync
    // quebraria lendo Nome/Cor/Icone (que despesa não tem), e pior,
    // UpdateAsync/DeleteAsync operariam sobre o item de despesa (apagando-o
    // de verdade). Mesmo bug já corrigido do lado de Expense (ver
    // DynamoDbExpenseRepository), agora espelhado aqui. "Tipo" não existia em
    // itens de categoria antes desta correção — por isso a ausência do
    // atributo também é aceita como categoria (compatibilidade com dado já
    // gravado em hom/prod), só a presença de um "Tipo" diferente rejeita.
    private const string TipoAttribute = "Tipo";
    private const string TipoCategoria = "categoria";

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbCategoryRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task<CategoryWriteResult> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        var item = BuildItem(category, BuildSk(category.Nome));

        try
        {
            await _dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(PK)"
            }, cancellationToken);

            return CategoryWriteResult.Success(category);
        }
        catch (ConditionalCheckFailedException)
        {
            return CategoryWriteResult.NameConflict();
        }
    }

    public async Task<IReadOnlyList<Category>> ListAsync(string accountId, string? tipo, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                [":skPrefix"] = new AttributeValue { S = SkPrefix }
            }
        }, cancellationToken);

        var categories = response.Items.Select(MapToCategory);
        if (tipo is not null)
            categories = categories.Where(c => c.Tipo == tipo);

        return categories.ToList();
    }

    public async Task<Category?> GetByIdAsync(string accountId, string categoryId, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return null;

        var (pk, sk) = lookup.Value;
        if (pk != $"ACCOUNT#{accountId}")
            return null;

        var current = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = pk },
                ["SK"] = new AttributeValue { S = sk }
            }
        }, cancellationToken);

        return current.IsItemSet && IsCategoriaItem(current.Item) ? MapToCategory(current.Item) : null;
    }

    public async Task<CategoryWriteResult> UpdateAsync(
        string accountId,
        string categoryId,
        string nome,
        string tipo,
        long? orcamentoMensalCents,
        CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return CategoryWriteResult.NotFound();

        var (pk, oldSk) = lookup.Value;
        if (pk != $"ACCOUNT#{accountId}")
            return CategoryWriteResult.NotFound();

        var current = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = pk },
                ["SK"] = new AttributeValue { S = oldSk }
            }
        }, cancellationToken);

        if (!current.IsItemSet)
            return CategoryWriteResult.NotFound(); // corrida: item excluído entre a Query e o GetItem

        if (!IsCategoriaItem(current.Item))
            return CategoryWriteResult.NotFound(); // id pertence a outro tipo de item (ex.: Expense) — mesmo GSI2

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var newSk = BuildSk(nome);
        var updated = Category.Restore(categoryId, accountId, nome, tipo, orcamentoMensalCents, createdAt);
        var newItem = BuildItem(updated, newSk);

        if (newSk == oldSk)
        {
            // Slug não mudou (ainda que a grafia enviada seja diferente): mesma chave física,
            // PutItem simples sobrescreve o item (mesmo padrão de SaveAsync em Expense).
            await _dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = newItem
            }, cancellationToken);

            return CategoryWriteResult.Success(updated);
        }

        // Slug mudou: SK muda, não dá pra UpdateItem in-place. Delete+Put atômico via
        // TransactWriteItems — o Put também é condicional (diferente da FEAT-08) para impedir
        // que duas renomeações concorrentes colidam no mesmo nome.
        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Delete = new Delete
                        {
                            TableName = _options.TableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = pk },
                                ["SK"] = new AttributeValue { S = oldSk }
                            },
                            ConditionExpression = "attribute_exists(PK)"
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = newItem,
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    }
                ]
            }, cancellationToken);

            return CategoryWriteResult.Success(updated);
        }
        catch (TransactionCanceledException ex)
        {
            var putFailed = ex.CancellationReasons is { Count: > 1 } reasons
                && reasons[1].Code == "ConditionalCheckFailed";

            return putFailed ? CategoryWriteResult.NameConflict() : CategoryWriteResult.NotFound();
        }
    }

    public async Task<bool> DeleteAsync(string accountId, string categoryId, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return false;

        var (pk, sk) = lookup.Value;
        if (pk != $"ACCOUNT#{accountId}")
            return false;

        try
        {
            await _dynamoDbClient.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _options.TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = pk },
                    ["SK"] = new AttributeValue { S = sk }
                },
                // "AND (attribute_not_exists(#tipo) OR #tipo = :tipo)" garante que só um item de
                // categoria é apagado — sem isso, um id de despesa passado por engano em
                // DELETE /categories/{id} apagaria a despesa de verdade (mesmo GSI2 compartilhado).
                // attribute_not_exists cobre categorias já gravadas antes desta correção (nunca
                // tiveram o atributo Tipo).
                ConditionExpression = "attribute_exists(PK) AND (attribute_not_exists(#tipo) OR #tipo = :tipo)",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#tipo"] = TipoAttribute },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":tipo"] = new AttributeValue { S = TipoCategoria }
                }
            }, cancellationToken);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    private async Task<(string Pk, string Sk)?> LookupByIdAsync(string categoryId, CancellationToken cancellationToken)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{categoryId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return null;

        return (lookup.Items[0]["PK"].S, lookup.Items[0]["SK"].S);
    }

    private static string BuildSk(string nome) => $"{SkPrefix}{CategorySlug.From(nome)}";

    // Ausência do atributo também conta como categoria — itens gravados antes
    // desta correção nunca tiveram "Tipo". Só uma presença explícita de outro
    // valor (ex.: "despesa") rejeita.
    private static bool IsCategoriaItem(Dictionary<string, AttributeValue> item) =>
        !item.TryGetValue(TipoAttribute, out var tipo) || tipo.S == TipoCategoria;

    private static Dictionary<string, AttributeValue> BuildItem(Category category, string sk)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{category.AccountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{category.Id}" },
            ["Nome"] = new AttributeValue { S = category.Nome },
            ["Tipo"] = new AttributeValue { S = TipoCategoria },
            ["TipoLancamento"] = new AttributeValue { S = category.Tipo },
            ["CreatedAt"] = new AttributeValue { S = category.CreatedAt.ToString("O") }
        };

        if (category.OrcamentoMensalCents is { } orcamento)
            item["OrcamentoMensalCents"] = new AttributeValue { N = orcamento.ToString(CultureInfo.InvariantCulture) };

        return item;
    }

    private static Category MapToCategory(Dictionary<string, AttributeValue> item)
    {
        var pk = item["PK"].S;
        var accountId = pk[(pk.IndexOf('#') + 1)..];
        var gsi2pk = item["GSI2PK"].S;
        var id = gsi2pk[(gsi2pk.IndexOf('#') + 1)..];
        var createdAt = DateTimeOffset.Parse(
            item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        // Ausência de TipoLancamento == categoria gravada antes desta feature (FEAT-21) —
        // tratada como "despesa" implícito, mesma postura defensiva já usada pro
        // discriminador Tipo acima (nenhuma categoria de receita existia antes desta feature).
        var tipo = item.TryGetValue("TipoLancamento", out var tipoAttr) ? tipoAttr.S : "despesa";
        var orcamentoMensalCents = item.TryGetValue("OrcamentoMensalCents", out var orcamentoAttr)
            ? long.Parse(orcamentoAttr.N, CultureInfo.InvariantCulture)
            : (long?)null;

        // Cor/Icone: se o item ainda os tiver (categoria gravada antes desta feature), são
        // simplesmente ignorados — não fazem mais parte de Category.
        return Category.Restore(id, accountId, item["Nome"].S, tipo, orcamentoMensalCents, createdAt);
    }
}
