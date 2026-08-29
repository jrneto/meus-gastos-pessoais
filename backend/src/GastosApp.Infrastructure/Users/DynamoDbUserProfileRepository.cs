using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Users;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Users;

public sealed class DynamoDbUserProfileRepository : IUserProfileRepository
{
    // UserProfile: PK=USER#<userId>, SK=PROFILE#
    // CpfPointer: PK=CPF#<cpf>, SK=CPF# — item-sentinela só pra barrar CPF
    // duplicado via ConditionExpression, mesmo padrão do AccountPointer (FEAT-19).
    private const string ProfileSk = "PROFILE#";
    private const string CpfPointerSk = "CPF#";

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbUserProfileRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task<CreateUserProfileResult> CreateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem // índice 0: CpfPointer — barra CPF duplicado
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"CPF#{profile.Cpf}" },
                                ["SK"] = new AttributeValue { S = CpfPointerSk },
                                ["UserId"] = new AttributeValue { S = profile.UserId }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    new TransactWriteItem // índice 1: UserProfile
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"USER#{profile.UserId}" },
                                ["SK"] = new AttributeValue { S = ProfileSk },
                                ["Name"] = new AttributeValue { S = profile.Name },
                                ["PhoneNumber"] = new AttributeValue { S = profile.PhoneNumber },
                                ["Cpf"] = new AttributeValue { S = profile.Cpf },
                                ["CreatedAt"] = new AttributeValue { S = profile.CreatedAt.ToString("O") }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    }
                ]
            }, cancellationToken);

            return new CreateUserProfileResult(CpfAlreadyExists: false);
        }
        catch (TransactionCanceledException ex)
        {
            var cpfPointerFailed = ex.CancellationReasons is { Count: > 0 } reasons
                && reasons[0].Code == "ConditionalCheckFailed";

            if (!cpfPointerFailed)
                throw; // índice 1 falhou (userId colidindo) — praticamente impossível, propaga

            return new CreateUserProfileResult(CpfAlreadyExists: true);
        }
    }

    public async Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"USER#{userId}" },
                ["SK"] = new AttributeValue { S = ProfileSk }
            }
        }, cancellationToken);

        if (!response.IsItemSet)
            return null;

        return UserProfile.Restore(
            userId,
            response.Item["Name"].S,
            response.Item["PhoneNumber"].S,
            response.Item["Cpf"].S,
            DateTimeOffset.Parse(response.Item["CreatedAt"].S));
    }
}
