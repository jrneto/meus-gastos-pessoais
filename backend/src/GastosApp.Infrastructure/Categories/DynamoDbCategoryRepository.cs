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

    public async Task<IReadOnlyList<Category>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"USER#{userId}" },
                [":skPrefix"] = new AttributeValue { S = SkPrefix }
            }
        }, cancellationToken);

        return response.Items.Select(MapToCategory).ToList();
    }

    public async Task<Category?> GetByIdAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return null;

        var (pk, sk) = lookup.Value;
        if (pk != $"USER#{userId}")
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

        return current.IsItemSet ? MapToCategory(current.Item) : null;
    }

    public async Task<CategoryWriteResult> UpdateAsync(
        string userId,
        string categoryId,
        string nome,
        string cor,
        string icone,
        CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return CategoryWriteResult.NotFound();

        var (pk, oldSk) = lookup.Value;
        if (pk != $"USER#{userId}")
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

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var newSk = BuildSk(nome);
        var updated = Category.Restore(categoryId, userId, nome, cor, icone, createdAt);
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

    public async Task<bool> DeleteAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupByIdAsync(categoryId, cancellationToken);
        if (lookup is null)
            return false;

        var (pk, sk) = lookup.Value;
        if (pk != $"USER#{userId}")
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
                ConditionExpression = "attribute_exists(PK)"
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

    private static Dictionary<string, AttributeValue> BuildItem(Category category, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{category.UserId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{category.Id}" },
            ["Nome"] = new AttributeValue { S = category.Nome },
            ["Cor"] = new AttributeValue { S = category.Cor },
            ["Icone"] = new AttributeValue { S = category.Icone },
            ["CreatedAt"] = new AttributeValue { S = category.CreatedAt.ToString("O") }
        };
    }

    private static Category MapToCategory(Dictionary<string, AttributeValue> item)
    {
        var pk = item["PK"].S;
        var userId = pk[(pk.IndexOf('#') + 1)..];
        var gsi2pk = item["GSI2PK"].S;
        var id = gsi2pk[(gsi2pk.IndexOf('#') + 1)..];
        var createdAt = DateTimeOffset.Parse(
            item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return Category.Restore(id, userId, item["Nome"].S, item["Cor"].S, item["Icone"].S, createdAt);
    }
}
