using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Domain.Transactions;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbTransactionRepositorySaveTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositorySaveTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbTransactionRepository(_dynamoDbClientMock, options);
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteTipoAsGivenValue_NotAsAConstant()
    {
        // Arrange
        var transaction = Transaction.Create(
            "account-1", "Salário", 500000, "category-2", "receita", new DateOnly(2025, 6, 15), "user-1");

        // Act
        await _repository.SaveAsync(transaction);

        // Assert
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r => r.Item["Tipo"].S == "receita"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldAlwaysWriteCreatedByUserId()
    {
        // Arrange
        var transaction = Transaction.Create(
            "account-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        // Act
        await _repository.SaveAsync(transaction);

        // Assert
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r => r.Item["CreatedByUserId"].S == "user-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteDateAttribute_NotExpenseDate()
    {
        // Arrange
        var transaction = Transaction.Create(
            "account-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        // Act
        await _repository.SaveAsync(transaction);

        // Assert
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["Date"].S == "2025-06-15"
                && !r.Item.ContainsKey("ExpenseDate")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldWriteExpectedKeysAndIndexAttributes()
    {
        // Arrange
        var transaction = Transaction.Create(
            "account-1", "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        // Act
        await _repository.SaveAsync(transaction);

        // Assert
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.TableName == "GastosApp-unitTests"
                && r.Item["PK"].S == $"ACCOUNT#account-1"
                && r.Item["SK"].S == $"TXN#2025-06-15#{transaction.Id}"
                && r.Item["GSI1PK"].S == "ACCOUNT#account-1#category-1"
                && r.Item["GSI1SK"].S == $"2025-06-15#{transaction.Id}"
                && r.Item["GSI2PK"].S == $"ID#{transaction.Id}"
                && r.Item["Description"].S == "Almoço"
                && r.Item["AmountInCents"].N == "4590"
                && r.Item["CategoryId"].S == "category-1"),
            Arg.Any<CancellationToken>());
    }
}
