using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbExpenseRepositoryUpdateTests
{
    private static readonly DateTimeOffset OriginalCreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbExpenseRepository _repository;

    public DynamoDbExpenseRepositoryUpdateTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbExpenseRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildKeyItem(string userId, string sk) => new()
    {
        ["PK"] = new AttributeValue { S = $"USER#{userId}" },
        ["SK"] = new AttributeValue { S = sk }
    };

    private static GetItemResponse BuildGetItemResponse(string userId, string sk) => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{userId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") },
            ["Tipo"] = new AttributeValue { S = "despesa" }
        }
    };

    private static GetItemResponse BuildNonDespesaGetItemResponse(string userId, string sk) => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"USER#{userId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") }
            // sem "Tipo" = "despesa" — simula colisão de GSI2PK com item de outro tipo.
        }
    };

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenGsi2QueryFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "expense-inexistente", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenExpenseBelongsToAnotherUser()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outro-user", "TXN#2025-06-15#expense-1")] });

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "expense-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenGetItemFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "expense-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenItemFoundIsNotAnExpense()
    {
        // Arrange — sem essa checagem, um categoryId passado por engano faria PutItem/
        // TransactWriteItems apagar a categoria e criar uma despesa no lugar dela.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "CAT#mercado")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildNonDespesaGetItemResponse("user-1", "CAT#mercado"));

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "category-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUsePutItem_WhenExpenseDateIsUnchanged()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("user-1", "TXN#2025-06-15#expense-1"));

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "expense-1", "Almoço atualizado", 5290, "category-1", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Almoço atualizado");
        result.AmountInCents.Should().Be(5290);
        result.CategoryId.Should().Be("category-1");
        result.CreatedAt.Should().Be(OriginalCreatedAt);

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["PK"].S == "USER#user-1"
                && r.Item["SK"].S == "TXN#2025-06-15#expense-1"
                && r.Item["Description"].S == "Almoço atualizado"
                && r.Item["CategoryId"].S == "category-1"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUseTransactWriteItems_WhenExpenseDateChanges()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("user-1", "TXN#2025-06-15#expense-1"));

        // Act
        var result = await _repository.UpdateAsync(
            "user-1", "expense-1", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 20));

        // Assert
        result.Should().NotBeNull();
        result!.ExpenseDate.Should().Be(new DateOnly(2025, 6, 20));
        result.CreatedAt.Should().Be(OriginalCreatedAt);

        await _dynamoDbClientMock.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(r =>
                r.TransactItems.Count == 2
                && r.TransactItems[0].Delete!.Key["SK"].S == "TXN#2025-06-15#expense-1"
                && r.TransactItems[0].Delete!.ConditionExpression == "attribute_exists(PK)"
                && r.TransactItems[1].Put!.Item["SK"].S == "TXN#2025-06-20#expense-1"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }
}
