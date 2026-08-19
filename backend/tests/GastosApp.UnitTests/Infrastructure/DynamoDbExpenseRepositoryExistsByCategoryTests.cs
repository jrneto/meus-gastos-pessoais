using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbExpenseRepositoryExistsByCategoryTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbExpenseRepository _repository;

    public DynamoDbExpenseRepositoryExistsByCategoryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbExpenseRepository(_dynamoDbClientMock, options);
    }

    [Fact]
    public async Task ExistsByCategoryAsync_ShouldQueryGsi1WithExpectedKey()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        await _repository.ExistsByCategoryAsync("user-1", "Alimentacao");

        // Assert
        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == "GSI1"
                && r.KeyConditionExpression == "GSI1PK = :gsi1pk"
                && r.ExpressionAttributeValues[":gsi1pk"].S == "USER#user-1#Alimentacao"
                && r.Limit == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsByCategoryAsync_ShouldReturnFalse_WhenNoItemsFound()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.ExistsByCategoryAsync("user-1", "Viagem");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByCategoryAsync_ShouldReturnTrue_WhenItemsFound()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "USER#user-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#expense-1" }
                }]
            });

        var result = await _repository.ExistsByCategoryAsync("user-1", "Alimentacao");

        result.Should().BeTrue();
    }
}
