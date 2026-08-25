using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Accounts;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Members;

public sealed class DynamoDbMembershipRepository : IMembershipRepository
{
    private const string SkPrefix = "MEMBER#";
    private const string Gsi1Index = "GSI1";

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbMembershipRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Membership>> ListAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                [":skPrefix"] = new AttributeValue { S = SkPrefix }
            }
        }, cancellationToken);

        return response.Items.Select(MapToMembership).ToList();
    }

    public async Task<Membership?> GetByIdAsync(string accountId, string membershipId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = ItemKey(accountId, membershipId)
        }, cancellationToken);

        return response.IsItemSet ? MapToMembership(response.Item) : null;
    }

    public async Task<Membership?> FindByAccountAndUserIdAsync(string accountId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi1Index,
            KeyConditionExpression = "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new AttributeValue { S = $"USER#{userId}" },
                [":gsi1sk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" }
            },
            Limit = 1
        }, cancellationToken);

        return response.Items.Count == 0 ? null : MapToMembership(response.Items[0]);
    }

    public async Task<MembershipWriteResult> CreateInviteAsync(
        string accountId, string email, MembershipRole role, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var existingMembers = await ListAsync(accountId, cancellationToken);
        if (existingMembers.Any(m => NormalizeEmail(m.Email) == normalizedEmail))
            return MembershipWriteResult.EmailConflict();

        var membership = Membership.CreateInvite(accountId, email, role);
        var item = BuildItem(membership, normalizedEmail);

        try
        {
            await _dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                TableName = _options.TableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(PK)"
            }, cancellationToken);

            return MembershipWriteResult.Success(membership);
        }
        catch (ConditionalCheckFailedException)
        {
            // Praticamente impossível (SK usa um GUID novo) — defesa, não é
            // este condicional quem realisticamente barra a corrida de convite
            // duplicado (isso já é resolvido pelo ListAsync acima).
            return MembershipWriteResult.EmailConflict();
        }
    }

    public async Task<MembershipWriteResult> UpdateRoleAsync(
        string accountId, string membershipId, MembershipRole role, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _options.TableName,
                Key = ItemKey(accountId, membershipId),
                UpdateExpression = "SET #role = :role",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#role"] = "Role" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":role"] = new AttributeValue { S = role.ToString() }
                },
                ConditionExpression = "attribute_exists(PK)",
                ReturnValues = ReturnValue.ALL_NEW
            }, cancellationToken);

            return MembershipWriteResult.Success(MapToMembership(response.Attributes));
        }
        catch (ConditionalCheckFailedException)
        {
            return MembershipWriteResult.NotFound();
        }
    }

    public async Task<bool> DeleteAsync(string accountId, string membershipId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDbClient.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _options.TableName,
                Key = ItemKey(accountId, membershipId),
                ConditionExpression = "attribute_exists(PK)"
            }, cancellationToken);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AcceptedInvite>> AcceptPendingInvitesByEmailAsync(
        string email, string userId, CancellationToken cancellationToken = default)
    {
        var pending = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            IndexName = Gsi1Index,
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":gsi1pk"] = new AttributeValue { S = $"EMAIL#{NormalizeEmail(email)}" }
            }
        }, cancellationToken);

        var accepted = new List<AcceptedInvite>();

        foreach (var item in pending.Items)
        {
            var pk = item["PK"].S;
            var accountId = pk[(pk.IndexOf('#') + 1)..];
            var membershipId = item["SK"].S[SkPrefix.Length..];
            var createdAt = DateTimeOffset.Parse(
                item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            // Sem ConditionExpression: só atributos mudam (PK/SK não mudam,
            // ver plan.md decisão técnica 2) — corrida de dois logins
            // concorrentes do mesmo e-mail é idempotente (mesmo resultado
            // final), não há dado inconsistente possível.
            await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _options.TableName,
                Key = ItemKey(accountId, membershipId),
                UpdateExpression = "SET #status = :status, UserId = :userId, GSI1PK = :gsi1pk",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = MembershipStatus.Ativo.ToString() },
                    [":userId"] = new AttributeValue { S = userId },
                    [":gsi1pk"] = new AttributeValue { S = $"USER#{userId}" }
                }
            }, cancellationToken);

            accepted.Add(new AcceptedInvite(accountId, createdAt));
        }

        return accepted;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static Dictionary<string, AttributeValue> ItemKey(string accountId, string membershipId) => new()
    {
        ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
        ["SK"] = new AttributeValue { S = $"{SkPrefix}{membershipId}" }
    };

    private static Dictionary<string, AttributeValue> BuildItem(Membership membership, string normalizedEmail)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{membership.AccountId}" },
            ["SK"] = new AttributeValue { S = $"{SkPrefix}{membership.Id}" },
            ["GSI1SK"] = new AttributeValue { S = $"ACCOUNT#{membership.AccountId}" },
            ["Email"] = new AttributeValue { S = membership.Email },
            ["Role"] = new AttributeValue { S = membership.Role.ToString() },
            ["Status"] = new AttributeValue { S = membership.Status.ToString() },
            ["CreatedAt"] = new AttributeValue { S = membership.CreatedAt.ToString("O") }
        };

        if (membership.UserId is not null)
        {
            item["UserId"] = new AttributeValue { S = membership.UserId };
            item["GSI1PK"] = new AttributeValue { S = $"USER#{membership.UserId}" };
        }
        else
        {
            item["GSI1PK"] = new AttributeValue { S = $"EMAIL#{normalizedEmail}" };
        }

        return item;
    }

    private static Membership MapToMembership(Dictionary<string, AttributeValue> item)
    {
        var pk = item["PK"].S;
        var accountId = pk[(pk.IndexOf('#') + 1)..];
        var membershipId = item["SK"].S[SkPrefix.Length..];
        var userId = item.TryGetValue("UserId", out var userIdValue) ? userIdValue.S : null;
        var createdAt = DateTimeOffset.Parse(
            item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return Membership.Restore(
            membershipId,
            accountId,
            userId,
            item["Email"].S,
            Enum.Parse<MembershipRole>(item["Role"].S),
            Enum.Parse<MembershipStatus>(item["Status"].S),
            createdAt);
    }
}
