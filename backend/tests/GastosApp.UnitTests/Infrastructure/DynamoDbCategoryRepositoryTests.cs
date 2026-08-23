using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Domain.Categories;
using GastosApp.Infrastructure.Categories;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbCategoryRepositoryTests
{
    private static readonly DateTimeOffset OriginalCreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbCategoryRepository _repository;

    public DynamoDbCategoryRepositoryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbCategoryRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildItem(
        string accountId, string sk, string id, string nome, string cor = "#0EA5E9", string icone = "plane") => new()
    {
        ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
        ["SK"] = new AttributeValue { S = sk },
        ["GSI2PK"] = new AttributeValue { S = $"ID#{id}" },
        ["Nome"] = new AttributeValue { S = nome },
        ["Cor"] = new AttributeValue { S = cor },
        ["Icone"] = new AttributeValue { S = icone },
        ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") }
    };

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_ShouldReturnSuccess_WhenPutItemSucceeds()
    {
        // Arrange
        var category = Category.Create("user-1", "Viagem", "#0EA5E9", "plane");
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await _repository.CreateAsync(category);

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["PK"].S == "ACCOUNT#user-1"
                && r.Item["SK"].S == "CAT#viagem"
                && r.Item["Nome"].S == "Viagem"
                && r.ConditionExpression == "attribute_not_exists(PK)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNameConflict_WhenConditionalCheckFails()
    {
        // Arrange
        var category = Category.Create("user-1", "Lazer", "#0EA5E9", "plane");
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<PutItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        // Act
        var result = await _repository.CreateAsync(category);

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NameConflict);
    }

    // ----- ListAsync -----

    [Fact]
    public async Task ListAsync_ShouldQueryByPkAndCatPrefix()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")]
            });

        // Act
        var result = await _repository.ListAsync("user-1");

        // Assert
        result.Should().ContainSingle();
        result[0].Nome.Should().Be("Viagem");

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "PK = :pk AND begins_with(SK, :skPrefix)"
                && r.ExpressionAttributeValues[":pk"].S == "ACCOUNT#user-1"
                && r.ExpressionAttributeValues[":skPrefix"].S == "CAT#"),
            Arg.Any<CancellationToken>());
    }

    // ----- GetByIdAsync -----

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.GetByIdAsync("user-1", "category-inexistente");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory_WhenBelongsToUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Viagem");
    }

    // ----- UpdateAsync -----

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.UpdateAsync("user-1", "category-inexistente", "Viagens", "#0EA5E9", "plane");

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Viagens", "#0EA5E9", "plane");

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUsePutItem_WhenSlugIsUnchanged()
    {
        // Arrange: "Viagem" -> "viagem", "Viagem!" também normaliza pra "viagem" (slug igual)
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });

        // Act
        var result = await _repository.UpdateAsync("user-1", "category-1", "Viagem!", "#F97316", "car");

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);
        result.Category!.CreatedAt.Should().Be(OriginalCreatedAt);

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r => r.Item["SK"].S == "CAT#viagem" && r.Item["Cor"].S == "#F97316"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUseTransactWriteItems_WhenSlugChanges()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "#0EA5E9", "plane");

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);

        await _dynamoDbClientMock.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(r =>
                r.TransactItems.Count == 2
                && r.TransactItems[0].Delete!.Key["SK"].S == "CAT#viagem"
                && r.TransactItems[0].Delete!.ConditionExpression == "attribute_exists(PK)"
                && r.TransactItems[1].Put!.Item["SK"].S == "CAT#lazer"
                && r.TransactItems[1].Put!.ConditionExpression == "attribute_not_exists(PK)"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNameConflict_WhenTransactionCanceledByPutCondition()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "None" },
                    new() { Code = "ConditionalCheckFailed" }
                }
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "#0EA5E9", "plane");

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NameConflict);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenTransactionCanceledByDeleteCondition()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "ConditionalCheckFailed" },
                    new() { Code = "None" }
                }
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "#0EA5E9", "plane");

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
    }

    // ----- DeleteAsync -----

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.DeleteAsync("user-1", "category-inexistente");

        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenCategoryBelongsToUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeTrue();
        await _dynamoDbClientMock.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(r =>
                r.Key["PK"].S == "ACCOUNT#user-1"
                && r.Key["SK"].S == "CAT#viagem"
                && r.ConditionExpression == "attribute_exists(PK)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenConditionalCheckFails()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeFalse();
    }
}
