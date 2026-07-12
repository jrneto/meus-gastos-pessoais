using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Expenses;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Expenses;

public sealed class DynamoDbExpenseRepository : IExpenseRepository
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbExpenseRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task SaveAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var yearMonth = expense.ExpenseDate.ToString("yyyy-MM");

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{expense.UserId}" },
            ["SK"] = new AttributeValue { S = $"TXN#{yearMonth}#{expense.Id}" },
            ["GSI1PK"] = new AttributeValue { S = $"USER#{expense.UserId}#{expense.Category}" },
            ["GSI1SK"] = new AttributeValue { S = $"{yearMonth}#{expense.Id}" },
            ["Description"] = new AttributeValue { S = expense.Description },
            ["AmountInCents"] = new AttributeValue { N = expense.AmountInCents.ToString() },
            ["Category"] = new AttributeValue { S = expense.Category.ToString() },
            ["ExpenseDate"] = new AttributeValue { S = expense.ExpenseDate.ToString("yyyy-MM-dd") },
            ["Tipo"] = new AttributeValue { S = "despesa" },
            ["CreatedAt"] = new AttributeValue { S = expense.CreatedAt.ToString("O") }
        };

        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _options.TableName,
            Item = item
        }, cancellationToken);
    }
}
