using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Accounts;
using GastosApp.Infrastructure.Configuration;
using GastosApp.Infrastructure.Members;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbMembershipRepositoryTests
{
    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbMembershipRepository _repository;

    public DynamoDbMembershipRepositoryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbMembershipRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildItem(
        string accountId, string membershipId, string email, string role, string status, string? userId, DateTimeOffset createdAt)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = $"MEMBER#{membershipId}" },
            ["GSI1SK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["Email"] = new AttributeValue { S = email },
            ["Role"] = new AttributeValue { S = role },
            ["Status"] = new AttributeValue { S = status },
            ["CreatedAt"] = new AttributeValue { S = createdAt.ToString("O") }
        };

        if (userId is not null)
        {
            item["UserId"] = new AttributeValue { S = userId };
            item["GSI1PK"] = new AttributeValue { S = $"USER#{userId}" };
        }
        else
        {
            item["GSI1PK"] = new AttributeValue { S = $"EMAIL#{email}" };
        }

        return item;
    }

    // ----- ListAsync -----

    [Fact]
    public async Task ListAsync_ShouldReturnAllMembers_MappedFromItems()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.QueryAsync(
                Arg.Is<QueryRequest>(r => r.KeyConditionExpression == "PK = :pk AND begins_with(SK, :skPrefix)"),
                Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [BuildItem("account-1", "membership-1", "titular@email.com", "Titular", "Ativo", "user-1", createdAt)]
            });

        var result = await _repository.ListAsync("account-1");

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("membership-1");
        result[0].Email.Should().Be("titular@email.com");
        result[0].Role.Should().Be(MembershipRole.Titular);
        result[0].Status.Should().Be(MembershipStatus.Ativo);
        result[0].UserId.Should().Be("user-1");
    }

    // ----- GetByIdAsync -----

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = false });

        var result = await _repository.GetByIdAsync("account-1", "membership-inexistente");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnMembership_WhenFound()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.GetItemAsync(
                Arg.Is<GetItemRequest>(r => r.Key["PK"].S == "ACCOUNT#account-1" && r.Key["SK"].S == "MEMBER#membership-1"),
                Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("account-1", "membership-1", "convidado@email.com", "Leitura", "ConvitePendente", null, createdAt)
            });

        var result = await _repository.GetByIdAsync("account-1", "membership-1");

        result.Should().NotBeNull();
        result!.Email.Should().Be("convidado@email.com");
        result.UserId.Should().BeNull();
        result.Status.Should().Be(MembershipStatus.ConvitePendente);
    }

    // ----- FindByAccountAndUserIdAsync -----

    [Fact]
    public async Task FindByAccountAndUserIdAsync_ShouldReturnNull_WhenNoMatch()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.FindByAccountAndUserIdAsync("account-1", "user-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByAccountAndUserIdAsync_ShouldQueryGsi1ByUserAndAccount()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.QueryAsync(
                Arg.Is<QueryRequest>(r =>
                    r.IndexName == "GSI1"
                    && r.KeyConditionExpression == "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk"
                    && r.ExpressionAttributeValues[":gsi1pk"].S == "USER#user-1"
                    && r.ExpressionAttributeValues[":gsi1sk"].S == "ACCOUNT#account-1"),
                Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [BuildItem("account-1", "membership-1", "titular@email.com", "Titular", "Ativo", "user-1", createdAt)]
            });

        var result = await _repository.FindByAccountAndUserIdAsync("account-1", "user-1");

        result.Should().NotBeNull();
        result!.Role.Should().Be(MembershipRole.Titular);
    }

    // ----- CreateInviteAsync -----

    [Fact]
    public async Task CreateInviteAsync_ShouldCreatePendingInvite_WhenEmailNotAlreadyMember()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        var result = await _repository.CreateInviteAsync("account-1", "convidado@email.com", MembershipRole.Leitura);

        result.Outcome.Should().Be(MembershipWriteOutcome.Success);
        result.Membership!.Email.Should().Be("convidado@email.com");
        result.Membership.Status.Should().Be(MembershipStatus.ConvitePendente);

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["Email"].S == "convidado@email.com"
                && r.Item["Role"].S == "Leitura"
                && r.Item["Status"].S == "ConvitePendente"
                && r.Item["GSI1PK"].S == "EMAIL#convidado@email.com"
                && !r.Item.ContainsKey("UserId")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateInviteAsync_ShouldReturnEmailConflict_WhenEmailAlreadyMember()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [BuildItem("account-1", "membership-1", "existente@email.com", "Titular", "Ativo", "user-1", createdAt)]
            });

        var result = await _repository.CreateInviteAsync("account-1", "Existente@Email.com", MembershipRole.Total);

        result.Outcome.Should().Be(MembershipWriteOutcome.EmailConflict);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }

    // ----- UpdateRoleAsync -----

    [Fact]
    public async Task UpdateRoleAsync_ShouldReturnUpdatedMembership_WhenFound()
    {
        var createdAt = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse
            {
                Attributes = BuildItem("account-1", "membership-1", "convidado@email.com", "Total", "ConvitePendente", null, createdAt)
            });

        var result = await _repository.UpdateRoleAsync("account-1", "membership-1", MembershipRole.Total);

        result.Outcome.Should().Be(MembershipWriteOutcome.Success);
        result.Membership!.Role.Should().Be(MembershipRole.Total);
    }

    [Fact]
    public async Task UpdateRoleAsync_ShouldReturnNotFound_WhenConditionFails()
    {
        _dynamoDbClientMock.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<UpdateItemResponse>(_ => throw new ConditionalCheckFailedException("não existe"));

        var result = await _repository.UpdateRoleAsync("account-1", "membership-inexistente", MembershipRole.Total);

        result.Outcome.Should().Be(MembershipWriteOutcome.NotFound);
    }

    // ----- DeleteAsync -----

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenDeleted()
    {
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        var result = await _repository.DeleteAsync("account-1", "membership-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenConditionFails()
    {
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("não existe"));

        var result = await _repository.DeleteAsync("account-1", "membership-inexistente");

        result.Should().BeFalse();
    }

    // ----- AcceptPendingInvitesByEmailAsync -----

    [Fact]
    public async Task AcceptPendingInvitesByEmailAsync_ShouldReturnEmpty_WhenNoPendingInvites()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.AcceptPendingInvitesByEmailAsync("user@email.com", "user-1");

        result.Should().BeEmpty();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().UpdateItemAsync(default!, default);
    }

    [Fact]
    public async Task AcceptPendingInvitesByEmailAsync_ShouldUpdateEachMatch_AndReturnAcceptedInvites()
    {
        var createdAt1 = DateTimeOffset.UtcNow.AddDays(-1);
        var createdAt2 = DateTimeOffset.UtcNow;
        _dynamoDbClientMock.QueryAsync(
                Arg.Is<QueryRequest>(r =>
                    r.IndexName == "GSI1"
                    && r.KeyConditionExpression == "GSI1PK = :gsi1pk"
                    && r.ExpressionAttributeValues[":gsi1pk"].S == "EMAIL#user@email.com"),
                Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items =
                [
                    BuildItem("account-1", "membership-1", "user@email.com", "Leitura", "ConvitePendente", null, createdAt1),
                    BuildItem("account-2", "membership-2", "user@email.com", "Total", "ConvitePendente", null, createdAt2)
                ]
            });
        _dynamoDbClientMock.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UpdateItemResponse());

        var result = await _repository.AcceptPendingInvitesByEmailAsync("user@email.com", "user-1");

        result.Should().HaveCount(2);
        result.Should().Contain(a => a.AccountId == "account-1" && a.CreatedAt == createdAt1);
        result.Should().Contain(a => a.AccountId == "account-2" && a.CreatedAt == createdAt2);

        await _dynamoDbClientMock.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(r =>
                r.Key["PK"].S == "ACCOUNT#account-1" && r.Key["SK"].S == "MEMBER#membership-1"
                && r.ExpressionAttributeValues[":status"].S == "Ativo"
                && r.ExpressionAttributeValues[":userId"].S == "user-1"
                && r.ExpressionAttributeValues[":gsi1pk"].S == "USER#user-1"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.Received(1).UpdateItemAsync(
            Arg.Is<UpdateItemRequest>(r => r.Key["PK"].S == "ACCOUNT#account-2" && r.Key["SK"].S == "MEMBER#membership-2"),
            Arg.Any<CancellationToken>());
    }
}
