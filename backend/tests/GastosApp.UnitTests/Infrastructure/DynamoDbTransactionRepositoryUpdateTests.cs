using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbTransactionRepositoryUpdateTests
{
    private static readonly DateTimeOffset OriginalCreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositoryUpdateTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbTransactionRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildKeyItem(string accountId, string sk) => new()
    {
        ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
        ["SK"] = new AttributeValue { S = sk }
    };

    private static GetItemResponse BuildGetItemResponse(string accountId, string sk, string createdByUserId = "user-original") => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") },
            ["CreatedByUserId"] = new AttributeValue { S = createdByUserId },
            ["Tipo"] = new AttributeValue { S = "despesa" }
        }
    };

    private static GetItemResponse BuildNonTransactionGetItemResponse(string accountId, string sk) => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") },
            ["Tipo"] = new AttributeValue { S = "categoria" }
            // Tipo = "categoria" — simula colisão de GSI2PK com item de outro tipo.
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
            "account-1", "transaction-inexistente", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenTransactionBelongsToAnotherAccount()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outra-conta", "TXN#2025-06-15#transaction-1")] });

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "transaction-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenGetItemFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "transaction-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenItemFoundIsNotATransaction()
    {
        // Arrange — sem essa checagem, um categoryId passado por engano faria PutItem/
        // TransactWriteItems apagar a categoria e criar uma transação no lugar dela.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "CAT#mercado")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildNonTransactionGetItemResponse("account-1", "CAT#mercado"));

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "category-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUsePutItem_WhenDateIsUnchanged()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("account-1", "TXN#2025-06-15#transaction-1"));

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "transaction-1", "Almoço atualizado", 5290, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Almoço atualizado");
        result.AmountInCents.Should().Be(5290);
        result.CategoryId.Should().Be("category-1");
        result.CreatedAt.Should().Be(OriginalCreatedAt);
        result.CreatedByUserId.Should().Be("user-original");

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["PK"].S == "ACCOUNT#account-1"
                && r.Item["SK"].S == "TXN#2025-06-15#transaction-1"
                && r.Item["Description"].S == "Almoço atualizado"
                && r.Item["CategoryId"].S == "category-1"
                && r.Item["CreatedByUserId"].S == "user-original"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUseTransactWriteItems_WhenDateChanges()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("account-1", "TXN#2025-06-15#transaction-1"));

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "transaction-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 20));

        // Assert
        result.Should().NotBeNull();
        result!.Date.Should().Be(new DateOnly(2025, 6, 20));
        result.CreatedAt.Should().Be(OriginalCreatedAt);

        await _dynamoDbClientMock.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(r =>
                r.TransactItems.Count == 2
                && r.TransactItems[0].Delete!.Key["SK"].S == "TXN#2025-06-15#transaction-1"
                && r.TransactItems[0].Delete!.ConditionExpression == "attribute_exists(PK)"
                && r.TransactItems[1].Put!.Item["SK"].S == "TXN#2025-06-20#transaction-1"
                && r.TransactItems[1].Put!.Item["CreatedByUserId"].S == "user-original"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveCreatedByUserId_RegardlessOfCaller()
    {
        // Arrange — o autor nunca muda numa edição, mesmo quando outro membro edita.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("account-1", "TXN#2025-06-15#transaction-1", createdByUserId: "autor-original"));

        // Act
        var result = await _repository.UpdateAsync(
            "account-1", "transaction-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        // Assert
        result!.CreatedByUserId.Should().Be("autor-original");
    }
}
