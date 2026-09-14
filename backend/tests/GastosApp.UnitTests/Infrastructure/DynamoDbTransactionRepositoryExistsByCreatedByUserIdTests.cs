using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

// FEAT-41 — RemoveMemberCommandHandler usa isso pra decidir inativar (em vez
// de remover de fato) um membro que já lançou transação.
public class DynamoDbTransactionRepositoryExistsByCreatedByUserIdTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositoryExistsByCreatedByUserIdTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbTransactionRepository(_dynamoDbClientMock, options);
    }

    [Fact]
    public async Task ExistsByCreatedByUserIdAsync_ShouldQueryBasePartitionWithFilterOnCreatedByUserId()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        await _repository.ExistsByCreatedByUserIdAsync("account-1", "user-2");

        // Assert
        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == null
                && r.KeyConditionExpression == "PK = :pk AND begins_with(SK, :skPrefix)"
                && r.FilterExpression == "CreatedByUserId = :userId"
                && r.ExpressionAttributeValues[":pk"].S == "ACCOUNT#account-1"
                && r.ExpressionAttributeValues[":skPrefix"].S == "TXN#"
                && r.ExpressionAttributeValues[":userId"].S == "user-2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsByCreatedByUserIdAsync_ShouldReturnFalse_WhenNoTransactionFound()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [], LastEvaluatedKey = null });

        var result = await _repository.ExistsByCreatedByUserIdAsync("account-1", "user-2");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByCreatedByUserIdAsync_ShouldReturnTrue_WhenTransactionFoundOnFirstPage()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#account-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#transaction-1" },
                    ["CreatedByUserId"] = new AttributeValue { S = "user-2" }
                }]
            });

        var result = await _repository.ExistsByCreatedByUserIdAsync("account-1", "user-2");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByCreatedByUserIdAsync_ShouldPaginate_UntilFindingAMatch()
    {
        // Arrange — primeira página vazia mas com LastEvaluatedKey, segunda
        // página já filtrada pelo DynamoDB traz o item do autor.
        var firstPageKey = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "ACCOUNT#account-1" },
            ["SK"] = new AttributeValue { S = "TXN#2025-06-15#transaction-1" }
        };
        _dynamoDbClientMock.QueryAsync(
                Arg.Is<QueryRequest>(r => r.ExclusiveStartKey == null),
                Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [], LastEvaluatedKey = firstPageKey });
        _dynamoDbClientMock.QueryAsync(
                Arg.Is<QueryRequest>(r => r.ExclusiveStartKey == firstPageKey),
                Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#account-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-16#transaction-2" },
                    ["CreatedByUserId"] = new AttributeValue { S = "user-2" }
                }]
            });

        var result = await _repository.ExistsByCreatedByUserIdAsync("account-1", "user-2");

        result.Should().BeTrue();
        await _dynamoDbClientMock.Received(2).QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsByCreatedByUserIdAsync_ShouldReturnFalse_WhenOnlyOtherAuthorHasTransactions()
    {
        // Arrange — FilterExpression já é aplicado pelo DynamoDB (server-side);
        // este teste simula o resultado já filtrado (sem item de user-2) pra
        // garantir que a paginação para corretamente sem falso positivo.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [], LastEvaluatedKey = null });

        var result = await _repository.ExistsByCreatedByUserIdAsync("account-1", "user-2");

        result.Should().BeFalse();
    }
}
