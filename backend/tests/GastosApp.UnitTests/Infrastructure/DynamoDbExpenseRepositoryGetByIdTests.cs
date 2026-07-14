using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Domain.Expenses;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbExpenseRepositoryGetByIdTests
{
    private static readonly DateTimeOffset CreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbExpenseRepository _repository;

    public DynamoDbExpenseRepositoryGetByIdTests()
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
            ["Description"] = new AttributeValue { S = "Almoço no restaurante" },
            ["AmountInCents"] = new AttributeValue { N = "4590" },
            ["Category"] = new AttributeValue { S = "Alimentacao" },
            ["ExpenseDate"] = new AttributeValue { S = "2025-06-15" },
            ["CreatedAt"] = new AttributeValue { S = CreatedAt.ToString("O") }
        }
    };

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGsi2QueryFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        // Act
        var result = await _repository.GetByIdAsync("user-1", "expense-inexistente");

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenExpenseBelongsToAnotherUser()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("outro-user", "TXN#2025-06-15#expense-1")] });

        // Act
        var result = await _repository.GetByIdAsync("user-1", "expense-1");

        // Assert
        result.Should().BeNull();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGetItemFindsNothing()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        // Act
        var result = await _repository.GetByIdAsync("user-1", "expense-1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnExpense_WhenFoundAndOwnedByUser()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [BuildKeyItem("user-1", "TXN#2025-06-15#expense-1")] });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(BuildGetItemResponse("user-1", "TXN#2025-06-15#expense-1"));

        // Act
        var result = await _repository.GetByIdAsync("user-1", "expense-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("expense-1");
        result.UserId.Should().Be("user-1");
        result.Description.Should().Be("Almoço no restaurante");
        result.AmountInCents.Should().Be(4590);
        result.Category.Should().Be(ExpenseCategory.Alimentacao);
        result.ExpenseDate.Should().Be(new DateOnly(2025, 6, 15));
        result.CreatedAt.Should().Be(CreatedAt);
    }
}
