using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Infrastructure.Accounts;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbAccountRepositoryTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbAccountRepository _repository;

    public DynamoDbAccountRepositoryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbAccountRepository(_dynamoDbClientMock, options);
    }

    // ----- FindAccountIdByUserIdAsync -----

    [Fact]
    public async Task FindAccountIdByUserIdAsync_ShouldReturnNull_WhenPointerDoesNotExist()
    {
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        var result = await _repository.FindAccountIdByUserIdAsync("user-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindAccountIdByUserIdAsync_ShouldReturnAccountId_WhenPointerExists()
    {
        _dynamoDbClientMock.GetItemAsync(
                Arg.Is<GetItemRequest>(r => r.Key["PK"].S == "USER#user-1" && r.Key["SK"].S == "ACCOUNT#"),
                Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "USER#user-1" },
                    ["SK"] = new AttributeValue { S = "ACCOUNT#" },
                    ["AccountId"] = new AttributeValue { S = "account-1" }
                }
            });

        var result = await _repository.FindAccountIdByUserIdAsync("user-1");

        result.Should().Be("account-1");
    }

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_ShouldWriteAccountPointerAccountAndMembership_WhenNoConflict()
    {
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var result = await _repository.CreateAsync("user-1");

        result.AlreadyExisted.Should().BeFalse();
        result.AccountId.Should().NotBeNullOrWhiteSpace();

        await _dynamoDbClientMock.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(r =>
                r.TransactItems.Count == 3
                // Item 0: AccountPointer — único cuja condição realmente serializa a concorrência.
                && r.TransactItems[0].Put!.Item["PK"].S == "USER#user-1"
                && r.TransactItems[0].Put!.Item["SK"].S == "ACCOUNT#"
                && r.TransactItems[0].Put!.Item["AccountId"].S == result.AccountId
                && r.TransactItems[0].Put!.ConditionExpression == "attribute_not_exists(PK)"
                // Item 1: Account
                && r.TransactItems[1].Put!.Item["PK"].S == $"ACCOUNT#{result.AccountId}"
                && r.TransactItems[1].Put!.Item["SK"].S == "ACCOUNT#"
                // Item 2: Membership (Titular)
                && r.TransactItems[2].Put!.Item["PK"].S == $"ACCOUNT#{result.AccountId}"
                && r.TransactItems[2].Put!.Item["SK"].S == "MEMBER#user-1"
                && r.TransactItems[2].Put!.Item["GSI1PK"].S == "USER#user-1"
                && r.TransactItems[2].Put!.Item["GSI1SK"].S == $"ACCOUNT#{result.AccountId}"
                && r.TransactItems[2].Put!.Item["Role"].S == "Titular"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldGenerateDifferentAccountIds_ForDifferentCalls()
    {
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var first = await _repository.CreateAsync("user-1");
        var second = await _repository.CreateAsync("user-2");

        first.AccountId.Should().NotBe(second.AccountId);
    }

    [Fact]
    public async Task CreateAsync_ShouldRecoverWinnerAccountId_WhenAccountPointerConditionFails()
    {
        // Arrange — corrida: outro caller (trigger do Cognito ou outro login
        // concorrente) já criou a Account desse usuário entre o
        // FindAccountIdByUserIdAsync e este Put. O AccountPointer (item 0)
        // é quem barra — recupera o AccountId do vencedor via GetItem em
        // vez de propagar erro (ver plan.md, seção 2/US7 da spec).
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "ConditionalCheckFailed" },
                    new() { Code = "None" },
                    new() { Code = "None" }
                }
            });
        _dynamoDbClientMock.GetItemAsync(
                Arg.Is<GetItemRequest>(r => r.Key["PK"].S == "USER#user-1" && r.Key["SK"].S == "ACCOUNT#"),
                Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "USER#user-1" },
                    ["SK"] = new AttributeValue { S = "ACCOUNT#" },
                    ["AccountId"] = new AttributeValue { S = "account-do-vencedor" }
                }
            });

        // Act
        var result = await _repository.CreateAsync("user-1");

        // Assert
        result.AlreadyExisted.Should().BeTrue();
        result.AccountId.Should().Be("account-do-vencedor");
    }

    [Fact]
    public async Task CreateAsync_ShouldRethrow_WhenTransactionCanceledForAnotherReason()
    {
        // Arrange — falha que não é a corrida esperada (ex.: throttling
        // genuíno do DynamoDB) precisa propagar, não ser engolida.
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "None" },
                    new() { Code = "ConditionalCheckFailed" },
                    new() { Code = "None" }
                }
            });

        var act = async () => await _repository.CreateAsync("user-1");

        await act.Should().ThrowAsync<TransactionCanceledException>();
    }
}
