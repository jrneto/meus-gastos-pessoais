using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Transactions;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Transactions;

public sealed class DynamoDbTransactionRepository : ITransactionRepository
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string BaseIndex = "Base";
    private const string Gsi1Index = "GSI1";
    private const string Gsi2Index = "GSI2";
    private const int MaxPaginationIterations = 25;

    // GSI2 (GSI2PK = "ID#{id}") é compartilhado com outros tipos de item de
    // conta (ex.: categorias, mesmo formato de chave) — sem esse
    // discriminador, um id de categoria passado a um endpoint de transação
    // encontraria o item errado (crash ao ler atributos que só transação tem,
    // ou pior: update/delete operando sobre o item errado). "Tipo" já era
    // gravado em todo item de transação (SaveAsync/UpdateAsync) mas nunca era
    // conferido na leitura — bug encontrado ao consultar GET /expenses/{id}
    // com um categoryId por engano (500 em vez de 404) — corrigido junto com
    // essa checagem.
    private const string TipoAttribute = "Tipo";
    private const string TipoCategoria = "categoria"; // valor gravado por Category — único valor que NÃO é uma Transaction

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbTransactionRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task SaveAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var day = transaction.Date.ToString(DateFormat);

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{transaction.AccountId}" },
            ["SK"] = new AttributeValue { S = $"TXN#{day}#{transaction.Id}" },
            ["GSI1PK"] = new AttributeValue { S = $"ACCOUNT#{transaction.AccountId}#{transaction.CategoryId}" },
            ["GSI1SK"] = new AttributeValue { S = $"{day}#{transaction.Id}" },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{transaction.Id}" },
            ["Description"] = new AttributeValue { S = transaction.Description },
            ["AmountInCents"] = new AttributeValue { N = transaction.AmountInCents.ToString() },
            ["CategoryId"] = new AttributeValue { S = transaction.CategoryId },
            ["Date"] = new AttributeValue { S = day },
            ["Tipo"] = new AttributeValue { S = transaction.Tipo },
            ["CreatedByUserId"] = new AttributeValue { S = transaction.CreatedByUserId },
            ["CreatedAt"] = new AttributeValue { S = transaction.CreatedAt.ToString("O") }
        };

        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _options.TableName,
            Item = item
        }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string accountId, string transactionId, CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{transactionId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return false;

        var pk = lookup.Items[0]["PK"].S;
        var sk = lookup.Items[0]["SK"].S;

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
                // "AND #tipo <> :tipoCategoria" garante que só um item de transação é
                // apagado (aceita "despesa" e "receita" sem enumerá-los) — sem isso, um
                // categoryId passado por engano em DELETE /transactions/{id} apagaria a categoria.
                ConditionExpression = "attribute_exists(PK) AND #tipo <> :tipoCategoria",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#tipo"] = TipoAttribute },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":tipoCategoria"] = new AttributeValue { S = TipoCategoria }
                }
            }, cancellationToken);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<Transaction?> GetByIdAsync(string accountId, string transactionId, CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{transactionId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return null;

        var pk = lookup.Items[0]["PK"].S;
        var sk = lookup.Items[0]["SK"].S;

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

        if (!current.IsItemSet || !IsTransactionItem(current.Item))
            return null;

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var date = DateOnly.ParseExact(current.Item["Date"].S, DateFormat, CultureInfo.InvariantCulture);
        var categoryId = current.Item["CategoryId"].S;
        var amountInCents = long.Parse(current.Item["AmountInCents"].N, CultureInfo.InvariantCulture);
        var description = current.Item["Description"].S;
        var tipo = current.Item["Tipo"].S;
        var createdByUserId = current.Item["CreatedByUserId"].S;

        return Transaction.Restore(transactionId, accountId, description, amountInCents, categoryId, tipo, date, createdByUserId, createdAt);
    }

    public async Task<Transaction?> UpdateAsync(
        string accountId,
        string transactionId,
        string description,
        long amountInCents,
        string categoryId,
        string tipo,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi2Index,
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi2pk"] = new AttributeValue { S = $"ID#{transactionId}" }
            },
            Limit = 1
        }, cancellationToken);

        if (lookup.Items.Count == 0)
            return null;

        var pk = lookup.Items[0]["PK"].S;
        var oldSk = lookup.Items[0]["SK"].S;

        if (pk != $"ACCOUNT#{accountId}")
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

        if (!current.IsItemSet || !IsTransactionItem(current.Item))
            return null;

        var createdAt = DateTimeOffset.Parse(
            current.Item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        // Autor nunca muda numa edição (regra de negócio) — preservado do item atual.
        var createdByUserId = current.Item["CreatedByUserId"].S;
        var newDay = date.ToString(DateFormat);
        var newSk = $"TXN#{newDay}#{transactionId}";

        var newItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = pk },
            ["SK"] = new AttributeValue { S = newSk },
            ["GSI1PK"] = new AttributeValue { S = $"{pk}#{categoryId}" },
            ["GSI1SK"] = new AttributeValue { S = $"{newDay}#{transactionId}" },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{transactionId}" },
            ["Description"] = new AttributeValue { S = description },
            ["AmountInCents"] = new AttributeValue { N = amountInCents.ToString() },
            ["CategoryId"] = new AttributeValue { S = categoryId },
            ["Date"] = new AttributeValue { S = newDay },
            ["Tipo"] = new AttributeValue { S = tipo },
            ["CreatedByUserId"] = new AttributeValue { S = createdByUserId },
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

        return Transaction.Restore(transactionId, accountId, description, amountInCents, categoryId, tipo, date, createdByUserId, createdAt);
    }

    // Generalização de "IsDespesaItem": aceita "despesa" e "receita" sem listar as
    // duas — qualquer Tipo diferente de "categoria" já é suficiente pra discriminar
    // uma Transaction de uma Category no GSI2 compartilhado.
    private static bool IsTransactionItem(Dictionary<string, AttributeValue> item) =>
        item.TryGetValue(TipoAttribute, out var tipo) && tipo.S != TipoCategoria;

    public async Task<bool> ExistsByCategoryAsync(string accountId, string categoryId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi1Index,
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}#{categoryId}" }
            },
            Limit = 1
        }, cancellationToken);

        return response.Items.Count > 0;
    }

    public async Task<TransactionQueryPage> QueryAsync(TransactionQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var index = filter.CategoryId is not null ? Gsi1Index : BaseIndex;

        Dictionary<string, AttributeValue>? exclusiveStartKey = null;
        if (filter.Cursor is not null && TransactionCursorCodec.TryDecode(filter.Cursor, out var cursorPayload))
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
                    "Número máximo de iterações de paginação excedido ao consultar transações.");
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

            nextCursor = TransactionCursorCodec.Encode(new TransactionCursorPayload(index, lastEvaluatedKey));
        }

        var items = pageItems.Select(MapToTransactionQueryItem).ToList();
        return new TransactionQueryPage(items, nextCursor);
    }

    private QueryRequest BuildQueryRequest(
        TransactionQueryFilter filter, string index, Dictionary<string, AttributeValue>? exclusiveStartKey)
    {
        var names = new Dictionary<string, string>();
        var values = new Dictionary<string, AttributeValue>();
        string keyConditionExpression;

        if (index == Gsi1Index)
        {
            names["#pk"] = "GSI1PK";
            values[":pk"] = new AttributeValue { S = $"ACCOUNT#{filter.AccountId}#{filter.CategoryId}" };
            keyConditionExpression = "#pk = :pk";

            var skCondition = BuildSkCondition(filter, names, values, skAttributeName: "GSI1SK", skPrefix: string.Empty);
            if (skCondition is not null)
                keyConditionExpression += " AND " + skCondition;
        }
        else
        {
            names["#pk"] = "PK";
            values[":pk"] = new AttributeValue { S = $"ACCOUNT#{filter.AccountId}" };
            keyConditionExpression = "#pk = :pk";

            var skCondition = BuildSkCondition(filter, names, values, skAttributeName: "SK", skPrefix: "TXN#");
            if (skCondition is not null)
                keyConditionExpression += " AND " + skCondition;
        }

        var filterExpression = BuildFilterExpression(filter, names, values);

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
        TransactionQueryFilter filter, Dictionary<string, string> names, Dictionary<string, AttributeValue> values,
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

        // Sem filtro de data/mês: no índice base a PK é compartilhada com outros tipos de item
        // (ex.: categorias). É preciso restringir explicitamente a SK a transações (prefixo "TXN#"),
        // senão a query retorna itens que não têm os atributos de transação e
        // MapToTransactionQueryItem quebra. No GSI1 isso não é necessário: só transações possuem GSI1PK.
        if (skPrefix.Length > 0)
        {
            values[":skPrefix"] = new AttributeValue { S = skPrefix };
            return "begins_with(#sk, :skPrefix)";
        }

        names.Remove("#sk");
        return null;
    }

    private static string? BuildFilterExpression(
        TransactionQueryFilter filter, Dictionary<string, string> names, Dictionary<string, AttributeValue> values)
    {
        var conditions = new List<string>();

        if (filter.Tipo is not null)
        {
            names["#tipo"] = "Tipo";
            values[":tipo"] = new AttributeValue { S = filter.Tipo };
            conditions.Add("#tipo = :tipo");
        }

        if (filter.MinAmountInCents is not null)
        {
            names["#amount"] = "AmountInCents";
            values[":minAmount"] = new AttributeValue { N = filter.MinAmountInCents.Value.ToString() };
            conditions.Add("#amount >= :minAmount");
        }

        if (filter.MaxAmountInCents is not null)
        {
            names["#amount"] = "AmountInCents";
            values[":maxAmount"] = new AttributeValue { N = filter.MaxAmountInCents.Value.ToString() };
            conditions.Add("#amount <= :maxAmount");
        }

        return conditions.Count == 0 ? null : string.Join(" AND ", conditions);
    }

    private static TransactionQueryItem MapToTransactionQueryItem(Dictionary<string, AttributeValue> item)
    {
        var sk = item["SK"].S;
        var id = sk[(sk.LastIndexOf('#') + 1)..];

        return new TransactionQueryItem(
            Id: id,
            Description: item["Description"].S,
            AmountInCents: long.Parse(item["AmountInCents"].N, CultureInfo.InvariantCulture),
            CategoryId: item["CategoryId"].S,
            Tipo: item["Tipo"].S,
            Date: DateOnly.ParseExact(item["Date"].S, DateFormat, CultureInfo.InvariantCulture),
            CreatedByUserId: item["CreatedByUserId"].S,
            CreatedAt: DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}
