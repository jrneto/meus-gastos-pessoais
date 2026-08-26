using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbTransactionRepositoryGetByIdTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositoryGetByIdTests()
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

    private static GetItemResponse BuildGetItemResponse(string accountId, string sk, string tipo = "despesa") => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["Description"] = new AttributeValue { S = "Almoço no restaurante" },
            ["AmountInCents"] = new AttributeValue { N = "4590" },
            ["CategoryId"] = new AttributeValue { S = "category-1" },
            ["Date"] = new AttributeValue { S = "2025-06-15" },
            ["CreatedByUserId"] = new AttributeValue { S = "user-1" },
            ["CreatedAt"] = new AttributeValue { S = CreatedAt.ToString("O") },
            ["Tipo"] = new AttributeValue { S = tipo }
        }
    };

    private static GetItemResponse BuildNonTransactionGetItemResponse(string accountId, string sk) => new()
    {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["Nome"] = new AttributeValue { S = "Mercado" },
            ["Tipo"] = new AttributeValue { S = "categoria" },
            ["CreatedAt"] = new AttributeValue { S = CreatedAt.ToString("O") }
            // Tipo = "categoria" — simula o item de outro tipo (categoria) encontrado
            // por colisão de GSI2PK (mesmo formato "ID#{id}" pros dois).
        }
    };

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGsi2QueryFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        var result = await _repository.GetByIdAsync("account-1", "transaction-inexistente");

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenTransactionBelongsToAnotherAccount()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outra-conta", "TXN#2025-06-15#transaction-1")] });

        // Act
        var result = await _repository.GetByIdAsync("account-1", "transaction-1");

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGetItemFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        // Act
        var result = await _repository.GetByIdAsync("account-1", "transaction-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenItemFoundIsNotATransaction()
    {
        // Arrange — GSI2PK "ID#{id}" é compartilhado com outros tipos de item (ex.: categoria);
        // passar o id de uma categoria não deve estourar exceção, deve virar 404 (null aqui).
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "CAT#mercado")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildNonTransactionGetItemResponse("account-1", "CAT#mercado"));

        // Act
        var result = await _repository.GetByIdAsync("account-1", "category-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTransaction_WhenFoundAndOwnedByAccount()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("account-1", "TXN#2025-06-15#transaction-1"));

        // Act
        var result = await _repository.GetByIdAsync("account-1", "transaction-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("transaction-1");
        result.AccountId.Should().Be("account-1");
        result.Description.Should().Be("Almoço no restaurante");
        result.AmountInCents.Should().Be(4590);
        result.CategoryId.Should().Be("category-1");
        result.Tipo.Should().Be("despesa");
        result.Date.Should().Be(new DateOnly(2025, 6, 15));
        result.CreatedByUserId.Should().Be("user-1");
        result.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTransaction_WhenTipoIsReceita()
    {
        // Arrange — IsTransactionItem aceita "despesa" e "receita" (qualquer valor != "categoria").
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("account-1", "TXN#2025-06-15#transaction-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("account-1", "TXN#2025-06-15#transaction-1", tipo: "receita"));

        // Act
        var result = await _repository.GetByIdAsync("account-1", "transaction-1");

        // Assert
        result.Should().NotBeNull();
        result!.Tipo.Should().Be("receita");
    }
}
