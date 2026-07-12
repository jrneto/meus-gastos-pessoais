using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Expenses;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Expenses;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbExpenseRepositoryQueryTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbExpenseRepository _repository;

    public DynamoDbExpenseRepositoryQueryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbExpenseRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildItem(
        string userId, string category, string day, string id,
        long amountInCents = 1000, string description = "Despesa") => new()
    {
        ["PK"] = new AttributeValue { S = $"USER#{userId}" },
        ["SK"] = new AttributeValue { S = $"TXN#{day}#{id}" },
        ["GSI1PK"] = new AttributeValue { S = $"USER#{userId}#{category}" },
        ["GSI1SK"] = new AttributeValue { S = $"{day}#{id}" },
        ["Description"] = new AttributeValue { S = description },
        ["AmountInCents"] = new AttributeValue { N = amountInCents.ToString() },
        ["Category"] = new AttributeValue { S = category },
        ["ExpenseDate"] = new AttributeValue { S = day },
        ["Tipo"] = new AttributeValue { S = "despesa" },
        ["CreatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
    };

    private void SetupSingleResponse(QueryResponse response) =>
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

    [Fact]
    public async Task QueryAsync_ShouldUseBaseTable_WhenCategoryIsAbsent()
    {
        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, null, null, null, 20);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == null
                && r.TableName == "GastosApp-unitTests"
                && r.KeyConditionExpression == "#pk = :pk"
                && r.ExpressionAttributeNames["#pk"] == "PK"
                && r.ExpressionAttributeValues[":pk"].S == "USER#user-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldUseGsi1_WhenCategoryIsPresent()
    {
        var filter = new ExpenseQueryFilter(
            "user-1", null, ExpenseCategory.Alimentacao, null, null, null, null, null, 20);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.IndexName == "GSI1"
                && r.KeyConditionExpression == "#pk = :pk"
                && r.ExpressionAttributeNames["#pk"] == "GSI1PK"
                && r.ExpressionAttributeValues[":pk"].S == "USER#user-1#Alimentacao"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldUseBeginsWith_WhenOnlyYearMonthIsInformed()
    {
        var filter = new ExpenseQueryFilter("user-1", "2025-06", null, null, null, null, null, null, 20);
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
    public async Task QueryAsync_ShouldUseExclusiveRangeCondition_WhenDateFromAndDateToAreInformed()
    {
        var filter = new ExpenseQueryFilter(
            "user-1", null, null, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10), null, null, null, 20);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "#pk = :pk AND #sk >= :skFrom AND #sk < :skTo"
                && r.ExpressionAttributeValues[":skFrom"].S == "TXN#2025-06-01"
                && r.ExpressionAttributeValues[":skTo"].S == "TXN#2025-06-11"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldPreferDateRangeOverYearMonth_WhenBothAreInformed()
    {
        var filter = new ExpenseQueryFilter(
            "user-1", "2025-06", null, new DateOnly(2025, 6, 5), new DateOnly(2025, 6, 10), null, null, null, 20);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "#pk = :pk AND #sk >= :skFrom AND #sk < :skTo"
                && !r.ExpressionAttributeValues.ContainsKey(":skPrefix")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ShouldBuildAmountFilterExpression_WhenAmountRangeIsInformed()
    {
        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, 1000, 5000, null, 20);
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
    public async Task QueryAsync_ShouldLoopAcrossPages_UntilLimitIsFilled()
    {
        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, null, null, null, 3);

        var item1 = BuildItem("user-1", "Outros", "2025-06-04", "id-1");
        var item2 = BuildItem("user-1", "Outros", "2025-06-03", "id-2");
        var item3 = BuildItem("user-1", "Outros", "2025-06-02", "id-3");
        var item4 = BuildItem("user-1", "Outros", "2025-06-01", "id-4");

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

        ExpenseCursorCodec.TryDecode(page.NextCursor!, out var payload).Should().BeTrue();
        payload!.Index.Should().Be("Base");
        payload.LastEvaluatedKey["SK"].Should().Be(item3["SK"].S);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnNullCursor_WhenAllDataIsExhaustedExactlyAtLimit()
    {
        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, null, null, null, 2);

        var item1 = BuildItem("user-1", "Outros", "2025-06-02", "id-1");
        var item2 = BuildItem("user-1", "Outros", "2025-06-01", "id-2");

        SetupSingleResponse(new QueryResponse { Items = [item1, item2], LastEvaluatedKey = null });

        var page = await _repository.QueryAsync(filter);

        page.Items.Should().HaveCount(2);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_ShouldThrow_WhenPaginationGuardIsExceeded()
    {
        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, null, null, null, 100);

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
        var cursor = ExpenseCursorCodec.Encode(new ExpenseCursorPayload(
            "Base",
            new Dictionary<string, string> { ["PK"] = "USER#user-1", ["SK"] = "TXN#2025-06-10#id-9" }));

        var filter = new ExpenseQueryFilter("user-1", null, null, null, null, null, null, cursor, 20);
        SetupSingleResponse(new QueryResponse { Items = [], LastEvaluatedKey = null });

        await _repository.QueryAsync(filter);

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.ExclusiveStartKey != null
                && r.ExclusiveStartKey["PK"].S == "USER#user-1"
                && r.ExclusiveStartKey["SK"].S == "TXN#2025-06-10#id-9"),
            Arg.Any<CancellationToken>());
    }
}
