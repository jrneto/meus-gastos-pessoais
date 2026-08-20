using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Expenses;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Expenses;

public sealed class DynamoDbExpenseRepository : IExpenseRepository
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string BaseIndex = "Base";
    private const string Gsi1Index = "GSI1";
    private const string Gsi2Index = "GSI2";
    private const int MaxPaginationIterations = 25;

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbExpenseRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task SaveAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var day = expense.ExpenseDate.ToString(DateFormat);

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{expense.UserId}" },
            ["SK"] = new AttributeValue { S = $"TXN#{day}#{expense.Id}" },
            ["GSI1PK"] = new AttributeValue { S = $"USER#{expense.UserId}#{expense.CategoryId}" },
            ["GSI1SK"] = new AttributeValue { S = $"{day}#{expense.Id}" },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{expense.Id}" },
            ["Description"] = new AttributeValue { S = expense.Description },
            ["AmountInCents"] = new AttributeValue { N = expense.AmountInCents.ToString() },
            ["CategoryId"] = new AttributeValue { S = expense.CategoryId },
            ["ExpenseDate"] = new AttributeValue { S = expense.ExpenseDate.ToString(DateFormat) },
            ["Tipo"] = new AttributeValue { S = "despesa" },
            ["CreatedAt"] = new AttributeValue { S = expense.CreatedAt.ToString("O") }
        };

        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _options.TableName,
            Item = item
        }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string userId, string expenseId, CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{expenseId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return false;

        var pk = lookup.Items[0]["PK"].S;
        var sk = lookup.Items[0]["SK"].S;

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

    public async Task<Expense?> GetByIdAsync(string userId, string expenseId, CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{expenseId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return null;

        var pk = lookup.Items[0]["PK"].S;
        var sk = lookup.Items[0]["SK"].S;

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

        if (!current.IsItemSet)
            return null;

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var expenseDate = DateOnly.ParseExact(current.Item["ExpenseDate"].S, DateFormat, CultureInfo.InvariantCulture);
        var categoryId = current.Item["CategoryId"].S;
        var amountInCents = long.Parse(current.Item["AmountInCents"].N, CultureInfo.InvariantCulture);
        var description = current.Item["Description"].S;

        return Expense.Restore(expenseId, userId, description, amountInCents, categoryId, expenseDate, createdAt);
    }

    public async Task<Expense?> UpdateAsync(
        string userId,
        string expenseId,
        string description,
        long amountInCents,
        string categoryId,
        DateOnly expenseDate,
        CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{expenseId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return null;

        var pk = lookup.Items[0]["PK"].S;
        var oldSk = lookup.Items[0]["SK"].S;

        if (pk != $"USER#{userId}")
            return null;

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
            return null;

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var newDay = expenseDate.ToString(DateFormat);
        var newSk = $"TXN#{newDay}#{expenseId}";

        var newItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = pk },
            ["SK"] = new AttributeValue { S = newSk },
            ["GSI1PK"] = new AttributeValue { S = $"{pk}#{categoryId}" },
            ["GSI1SK"] = new AttributeValue { S = $"{newDay}#{expenseId}" },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{expenseId}" },
            ["Description"] = new AttributeValue { S = description },
            ["AmountInCents"] = new AttributeValue { N = amountInCents.ToString() },
            ["CategoryId"] = new AttributeValue { S = categoryId },
            ["ExpenseDate"] = new AttributeValue { S = newDay },
            ["Tipo"] = new AttributeValue { S = "despesa" },
            ["CreatedAt"] = new AttributeValue { S = createdAt.ToString("O") }
        };

        if (newSk == oldSk)
        {
            await _dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = newItem
            }, cancellationToken);
        }
        else
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
                        Put = new Put { TableName = _options.TableName, Item = newItem }
                    }
                ]
            }, cancellationToken);
        }

        return Expense.Restore(expenseId, userId, description, amountInCents, categoryId, expenseDate, createdAt);
    }

    public async Task<bool> ExistsByCategoryAsync(string userId, string categoryId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi1Index,
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new AttributeValue { S = $"USER#{userId}#{categoryId}" }
            },
            Limit = 1
        }, cancellationToken);

        return response.Items.Count > 0;
    }

    public async Task<ExpenseQueryPage> QueryAsync(ExpenseQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var index = filter.CategoryId is not null ? Gsi1Index : BaseIndex;

        Dictionary<string, AttributeValue>? exclusiveStartKey = null;
        if (filter.Cursor is not null && ExpenseCursorCodec.TryDecode(filter.Cursor, out var cursorPayload))
        {
            exclusiveStartKey = cursorPayload!.LastEvaluatedKey.ToDictionary(
                kv => kv.Key,
                kv => new AttributeValue { S = kv.Value });
        }

        var collected = new List<Dictionary<string, AttributeValue>>();
        var iterations = 0;

        while (true)
        {
            iterations++;
            if (iterations > MaxPaginationIterations)
            {
                throw new InvalidOperationException(
                    "Número máximo de iterações de paginação excedido ao consultar despesas.");
            }

            var request = BuildQueryRequest(filter, index, exclusiveStartKey);
            var response = await _dynamoDbClient.QueryAsync(request, cancellationToken);

            collected.AddRange(response.Items);
            exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;

            if (collected.Count >= filter.Limit || exclusiveStartKey is null)
                break;
        }

        var hasMore = collected.Count > filter.Limit || exclusiveStartKey is not null;
        var pageItems = collected.Take(filter.Limit).ToList();

        string? nextCursor = null;
        if (hasMore && pageItems.Count > 0)
        {
            var lastItem = pageItems[^1];
            var lastEvaluatedKey = new Dictionary<string, string>
            {
                ["PK"] = lastItem["PK"].S,
                ["SK"] = lastItem["SK"].S
            };

            if (index == Gsi1Index)
            {
                lastEvaluatedKey["GSI1PK"] = lastItem["GSI1PK"].S;
                lastEvaluatedKey["GSI1SK"] = lastItem["GSI1SK"].S;
            }

            nextCursor = ExpenseCursorCodec.Encode(new ExpenseCursorPayload(index, lastEvaluatedKey));
        }

        var items = pageItems.Select(MapToExpenseQueryItem).ToList();
        return new ExpenseQueryPage(items, nextCursor);
    }

    private QueryRequest BuildQueryRequest(
        ExpenseQueryFilter filter, string index, Dictionary<string, AttributeValue>? exclusiveStartKey)
    {
        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();
        string keyConditionExpression;

        if (index == Gsi1Index)
        {
            names["#pk"] = "GSI1PK";
            values[":pk"] = new AttributeValue { S = $"USER#{filter.UserId}#{filter.CategoryId}" };
            keyConditionExpression = "#pk = :pk";

            var skCondition = BuildSkCondition(filter, names, values, skAttributeName: "GSI1SK", skPrefix: string.Empty);
            if (skCondition is not null)
                keyConditionExpression += " AND " + skCondition;
        }
        else
        {
            names["#pk"] = "PK";
            values[":pk"] = new AttributeValue { S = $"USER#{filter.UserId}" };
            keyConditionExpression = "#pk = :pk";

            var skCondition = BuildSkCondition(filter, names, values, skAttributeName: "SK", skPrefix: "TXN#");
            if (skCondition is not null)
                keyConditionExpression += " AND " + skCondition;
        }

        var filterExpression = BuildAmountFilterExpression(filter, names, values);

        return new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = index == Gsi1Index ? "GSI1" : null,
            KeyConditionExpression = keyConditionExpression,
            FilterExpression = filterExpression,
            ExpressionAttributeNames = names,
            ExpressionAttributeValues = values,
            ExclusiveStartKey = exclusiveStartKey,
            ScanIndexForward = false,
            Limit = filter.Limit
        };
    }

    private static string? BuildSkCondition(
        ExpenseQueryFilter filter, Dictionary<string, string> names, Dictionary<string, AttributeValue> values,
        string skAttributeName, string skPrefix)
    {
        names["#sk"] = skAttributeName;

        // Intervalo de datas prevalece sobre yearMonth quando ambos presentes (decisão registrada no plan.md).
        if (filter.DateFrom is not null || filter.DateTo is not null)
        {
            if (filter.DateFrom is not null && filter.DateTo is not null)
            {
                // KeyConditionExpression aceita só uma condição por chave — não é possível combinar
                // ">=" e "<" com AND (erro do DynamoDB: "must only contain one condition per key").
                // BETWEEN é inclusivo nos dois limites; "~" (0x7E, maior que dígitos/hífen/"#" usados
                // na SK) garante que o limite superior cubra toda a SK do dia de dateTo (que tem sufixo
                // "#{id}" após a data), sem incluir o dia seguinte.
                values[":skFrom"] = new AttributeValue { S = $"{skPrefix}{filter.DateFrom.Value.ToString(DateFormat)}" };
                values[":skTo"] = new AttributeValue { S = $"{skPrefix}{filter.DateTo.Value.ToString(DateFormat)}~" };
                return "#sk BETWEEN :skFrom AND :skTo";
            }

            if (filter.DateFrom is not null)
            {
                values[":skFrom"] = new AttributeValue { S = $"{skPrefix}{filter.DateFrom.Value.ToString(DateFormat)}" };
                return "#sk >= :skFrom";
            }

            values[":skTo"] = new AttributeValue { S = $"{skPrefix}{filter.DateTo!.Value.AddDays(1).ToString(DateFormat)}" };
            return "#sk < :skTo";
        }

        if (filter.YearMonth is not null)
        {
            values[":skPrefix"] = new AttributeValue { S = $"{skPrefix}{filter.YearMonth}" };
            return "begins_with(#sk, :skPrefix)";
        }

        names.Remove("#sk");
        return null;
    }

    private static string? BuildAmountFilterExpression(
        ExpenseQueryFilter filter, Dictionary<string, string> names, Dictionary<string, AttributeValue> values)
    {
        if (filter.MinAmountInCents is null && filter.MaxAmountInCents is null)
            return null;

        names["#amount"] = "AmountInCents";
        var conditions = new List<string>();

        if (filter.MinAmountInCents is not null)
        {
            values[":minAmount"] = new AttributeValue { N = filter.MinAmountInCents.Value.ToString() };
            conditions.Add("#amount >= :minAmount");
        }

        if (filter.MaxAmountInCents is not null)
        {
            values[":maxAmount"] = new AttributeValue { N = filter.MaxAmountInCents.Value.ToString() };
            conditions.Add("#amount <= :maxAmount");
        }

        return string.Join(" AND ", conditions);
    }

    private static ExpenseQueryItem MapToExpenseQueryItem(Dictionary<string, AttributeValue> item)
    {
        var sk = item["SK"].S;
        var id = sk[(sk.LastIndexOf('#') + 1)..];

        return new ExpenseQueryItem(
            Id: id,
            Description: item["Description"].S,
            AmountInCents: long.Parse(item["AmountInCents"].N, CultureInfo.InvariantCulture),
            CategoryId: item["CategoryId"].S,
            ExpenseDate: DateOnly.ParseExact(item["ExpenseDate"].S, DateFormat, CultureInfo.InvariantCulture),
            CreatedAt: DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}
