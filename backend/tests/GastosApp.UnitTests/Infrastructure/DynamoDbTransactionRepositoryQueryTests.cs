using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Transactions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbTransactionRepositoryQueryTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbTransactionRepository _repository;

    public DynamoDbTransactionRepositoryQueryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbTransactionRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildItem(
        string accountId, string category, string day, string id,
        long amountInCents = 1000, string description = "Transação", string tipo = "despesa") => new()
    {
        ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
        ["SK"] = new AttributeValue { S = $"TXN#{day}#{id}" },
        ["GSI1PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}#{category}" },
        ["GSI1SK"] = new AttributeValue { S = $"{day}#{id}" },
        ["Description"] = new AttributeValue { S = description },
        ["AmountInCents"] = new AttributeValue { N = amountInCents.ToString() },
        ["CategoryId"] = new AttributeValue { S = category },
        ["Date"] = new AttributeValue { S = day },
        ["Tipo"] = new AttributeValue { S = tipo },
        ["CreatedByUserId"] = new AttributeValue { S = "user-1" },
        ["CreatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
    };

    private void SetupSingleResponse(QueryResponse response) =>
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

    private static TransactionQueryFilter Filter(
        string accountId = "account-1",
        string? tipo = null,
        string? yearMonth = null,
        string? categoryId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        long? minAmountInCents = null,
        long? maxAmountInCents = null,
        string? cursor = null,
        int limit = 20) =>
        new(accountId, tipo, yearMonth, categoryId, dateFrom, dateTo, minAmountInCents, maxAmountInCents, cursor, limit);

    [Fact]
    public async Task QueryAsync_ShouldUseBaseTable_WhenCategoryIsAbsent()
    {
        var filter = Filter();
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == null
                && r.TableName == "GastosApp-unitTests"
                && r.KeyConditionExpression == "#pk = :pk AND begins_with(#sk, :skPrefix)"
                && r.ExpressionAttributeNames["#pk"] == "PK"
                && r.ExpressionAttributeNames["#sk"] == "SK"
                && r.ExpressionAttributeValues[":pk"].S == "ACCOUNT#account-1"
                && r.ExpressionAttributeValues[":skPrefix"].S == "TXN#"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldUseGsi1_WhenCategoryIsPresent()
    {
        var filter = Filter(categoryId: "category-1");
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == "GSI1"
                && r.KeyConditionExpression == "#pk = :pk"
                && r.ExpressionAttributeNames["#pk"] == "GSI1PK"
                && r.ExpressionAttributeValues[":pk"].S == "ACCOUNT#account-1#category-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldUseBeginsWith_WhenOnlyYearMonthIsInformed()
    {
        var filter = Filter(yearMonth: "2025-06");
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "#pk = :pk AND begins_with(#sk, :skPrefix)"
                && r.ExpressionAttributeNames["#sk"] == "SK"
                && r.ExpressionAttributeValues[":skPrefix"].S == "TXN#2025-06"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldUseBetweenRangeCondition_WhenDateFromAndDateToAreInformed()
    {
        var filter = Filter(dateFrom: new DateOnly(2025, 6, 1), dateTo: new DateOnly(2025, 6, 10));
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "#pk = :pk AND #sk BETWEEN :skFrom AND :skTo"
                && r.ExpressionAttributeValues[":skFrom"].S == "TXN#2025-06-01"
                && r.ExpressionAttributeValues[":skTo"].S == "TXN#2025-06-10~"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldPreferDateRangeOverYearMonth_WhenBothAreInformed()
    {
        var filter = Filter(yearMonth: "2025-06", dateFrom: new DateOnly(2025, 6, 5), dateTo: new DateOnly(2025, 6, 10));
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "#pk = :pk AND #sk BETWEEN :skFrom AND :skTo"
                && !r.ExpressionAttributeValues.ContainsKey(":skPrefix")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldBuildAmountFilterExpression_WhenAmountRangeIsInformed()
    {
        var filter = Filter(minAmountInCents: 1000, maxAmountInCents: 5000);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.FilterExpression == "#amount >= :minAmount AND #amount <= :maxAmount"
                && r.ExpressionAttributeNames["#amount"] == "AmountInCents"
                && r.ExpressionAttributeValues[":minAmount"].N == "1000"
                && r.ExpressionAttributeValues[":maxAmount"].N == "5000"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldBuildTipoFilterExpression_WhenTipoIsInformed()
    {
        var filter = Filter(tipo: "receita");
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.FilterExpression == "#tipo = :tipo"
                && r.ExpressionAttributeNames["#tipo"] == "Tipo"
                && r.ExpressionAttributeValues[":tipo"].S == "receita"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldCombineTipoAndAmountFilterExpressions_WhenBothAreInformed()
    {
        var filter = Filter(tipo: "despesa", minAmountInCents: 1000, maxAmountInCents: 5000);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.FilterExpression == "#tipo = :tipo AND #amount >= :minAmount AND #amount <= :maxAmount"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldLoopAcrossPages_UntilLimitIsFilled()
    {
        var filter = Filter(limit: 3);

        var item1 = BuildItem("account-1", "Outros", "2025-06-04", "id-1");
        var item2 = BuildItem("account-1", "Outros", "2025-06-03", "id-2");
        var item3 = BuildItem("account-1", "Outros", "2025-06-02", "id-3");
        var item4 = BuildItem("account-1", "Outros", "2025-06-01", "id-4");

        var firstPageLastKey = new Dictionary<string, AttributeValue>
        {
            ["PK"] = item2["PK"], ["SK"] = item2["SK"]
        };

        var responses = new Queue<QueryResponse>(
        [
            new QueryResponse { Items = [item1, item2], LastEvaluatedKey = firstPageLastKey },
            new QueryResponse { Items = [item3, item4], LastEvaluatedKey = null }
        ]);

        var capturedRequests = new List<QueryRequest>();
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRequests.Add(callInfo.Arg<QueryRequest>());
                return responses.Dequeue();
            });

        var page = await _repository.QueryAsync(filter);

        capturedRequests.Should().HaveCount(2);
        capturedRequests[1].ExclusiveStartKey.Should().BeSameAs(firstPageLastKey);

        page.Items.Should().HaveCount(3);
        page.Items[0].Id.Should().Be("id-1");
        page.Items[1].Id.Should().Be("id-2");
        page.Items[2].Id.Should().Be("id-3");
        page.NextCursor.Should().NotBeNull();

        TransactionCursorCodec.TryDecode(page.NextCursor!, out var payload).Should().BeTrue();
        payload!.Index.Should().Be("Base");
        payload.LastEvaluatedKey["SK"].Should().Be(item3["SK"].S);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnNullCursor_WhenAllDataIsExhaustedExactlyAtLimit()
    {
        var filter = Filter(limit: 2);

        var item1 = BuildItem("account-1", "Outros", "2025-06-02", "id-1");
        var item2 = BuildItem("account-1", "Outros", "2025-06-01", "id-2");

        SetupSingleResponse(new QueryResponse { Items = [item1, item2], LastEvaluatedKey = null });

        var page = await _repository.QueryAsync(filter);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_ShouldThrow_WhenPaginationGuardIsExceeded()
    {
        var filter = Filter(limit: 100);

        var neverEndingKey = new Dictionary<string, AttributeValue> { ["PK"] = new AttributeValue { S = "x" } };
        var callCount = 0;

        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new QueryResponse { Items = [], LastEvaluatedKey = neverEndingKey };
            });

        var act = async () => await _repository.QueryAsync(filter);

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(25);
    }

    [Fact]
    public async Task QueryAsync_ShouldDecodeCursorIntoExclusiveStartKey_WhenCursorIsInformed()
    {
        var cursor = TransactionCursorCodec.Encode(new TransactionCursorPayload(
            "Base",
            new Dictionary<string, string> { ["PK"] = "ACCOUNT#account-1", ["SK"] = "TXN#2025-06-10#id-9" }));

        var filter = Filter(cursor: cursor);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.ExclusiveStartKey != null
                && r.ExclusiveStartKey["PK"].S == "ACCOUNT#account-1"
                && r.ExclusiveStartKey["SK"].S == "TXN#2025-06-10#id-9"),
            Arg.Any<CancellationToken>());
    }
}
