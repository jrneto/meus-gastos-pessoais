using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbExpenseRepositoryDeleteTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbExpenseRepository _repository;

    public DynamoDbExpenseRepositoryDeleteTests()
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

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenGsi2QueryFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        var result = await _repository.DeleteAsync("user-1", "expense-inexistente");

        // Assert
        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldQueryGsi2WithExpenseId()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        await _repository.DeleteAsync("user-1", "expense-1");

        // Assert
        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == "GSI2"
                && r.KeyConditionExpression == "GSI2PK = :gsi2pk"
                && r.ExpressionAttributeValues[":gsi2pk"].S == "ID#expense-1"
                && r.Limit == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenExpenseBelongsToAnotherUser()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outro-user", "TXN#2025-06-15#expense-1")] });

        // Act
        var result = await _repository.DeleteAsync("user-1", "expense-1");

        // Assert
        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteWithExactKeyAndCondition_WhenExpenseBelongsToUser()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        var result = await _repository.DeleteAsync("user-1", "expense-1");

        // Assert
        result.Should().BeTrue();
        await _dynamoDbClientMock.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(r =>
                r.TableName == "GastosApp-unitTests"
                && r.Key["PK"].S == "USER#user-1"
                && r.Key["SK"].S == "TXN#2025-06-15#expense-1"
                && r.ConditionExpression == "attribute_exists(PK)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenDeleteItemFailsConditionCheck()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        // Act
        var result = await _repository.DeleteAsync("user-1", "expense-1");

        // Assert
        result.Should().BeFalse();
    }
}
