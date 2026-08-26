using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbTransactionRepositoryDeleteTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositoryDeleteTests()
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

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenGsi2QueryFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        var result = await _repository.DeleteAsync("account-1", "transaction-inexistente");

        // Assert
        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldQueryGsi2WithTransactionId()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        await _repository.DeleteAsync("account-1", "transaction-1");

        // Assert
        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == "GSI2"
                && r.KeyConditionExpression == "GSI2PK = :gsi2pk"
                && r.ExpressionAttributeValues[":gsi2pk"].S == "ID#transaction-1"
                && r.Limit == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenTransactionBelongsToAnotherAccount()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outra-conta", "TXN#2025-06-15#transaction-1")] });

        // Act
        var result = await _repository.DeleteAsync("account-1", "transaction-1");

        // Assert
        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteWithExactKeyAndCondition_WhenTransactionBelongsToAccount()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        // Act
        var result = await _repository.DeleteAsync("account-1", "transaction-1");

        // Assert
        result.Should().BeTrue();
        await _dynamoDbClientMock.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(r =>
                r.TableName == "GastosApp-unitTests"
                && r.Key["PK"].S == "ACCOUNT#account-1"
                && r.Key["SK"].S == "TXN#2025-06-15#transaction-1"
                && r.ConditionExpression == "attribute_exists(PK) AND #tipo <> :tipoCategoria"
                && r.ExpressionAttributeNames!["#tipo"] == "Tipo"
                && r.ExpressionAttributeValues![":tipoCategoria"].S == "categoria"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenDeleteItemFailsConditionCheck()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        // Act
        var result = await _repository.DeleteAsync("account-1", "transaction-1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenItemFoundIsNotATransaction()
    {
        // Arrange — GSI2PK "ID#{id}" é compartilhado com outros tipos de item (ex.: categoria).
        // O ConditionExpression "#tipo <> :tipoCategoria" faz o DynamoDB falhar a condição
        // nesse caso (simulado aqui via ConditionalCheckFailedException), sem round-trip
        // extra de leitura, evitando que um categoryId passado por engano apague a categoria.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "CAT#mercado")] });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        // Act
        var result = await _repository.DeleteAsync("account-1", "category-1");

        // Assert
        result.Should().BeFalse();
    }
}
