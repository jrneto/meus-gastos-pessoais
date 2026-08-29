using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Categories;
using GastosApp.Infrastructure.Categories;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Accounts;

public sealed class DynamoDbAccountRepository : IAccountRepository
{
    // AccountPointer: PK=USER#<userId>, SK=ACCOUNT# — item determinístico a
    // partir só do userId (calculável antes de gerar o accountId), é o único
    // que realmente serializa a concorrência na criação (ver plan.md, seção 2).
    // Também é usado como resolução (GetItem direto, mais barato que Query).
    private const string AccountPointerSk = "ACCOUNT#";
    private const string MemberSkPrefix = "MEMBER#";
    private const string TitularRole = "Titular";
    private const string ActiveStatus = "Ativo";

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbAccountRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task<string?> FindAccountIdByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = AccountPointerKey(userId)
        }, cancellationToken);

        return response.IsItemSet ? response.Item["AccountId"].S : null;
    }

    public async Task<CreateAccountResult> CreateAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        var accountId = Guid.NewGuid().ToString();
        var membershipId = Guid.NewGuid().ToString();
        var createdAtOffset = DateTimeOffset.UtcNow;
        var createdAt = createdAtOffset.ToString("O");

        // Categorias padrão (FEAT-28): semeadas na mesma transação que cria
        // Account/Membership, nunca separadas — ver plan.md, decisão técnica 1.
        // Ou a conta nasce completa (com as 13 categorias), ou a criação
        // inteira cancela e é re-tentada do zero no próximo login/trigger.
        var defaultCategoryItems = DefaultCategorySeed.Items.Select(seed =>
        {
            var category = Category.Restore(seed.Id, accountId, seed.Nome, DefaultCategorySeed.Tipo, null, createdAtOffset);
            var sk = CategoryItemMapper.BuildSk(seed.Nome);

            return new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _options.TableName,
                    Item = CategoryItemMapper.BuildItem(category, sk),
                    ConditionExpression = "attribute_not_exists(PK)"
                }
            };
        });

        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>(AccountPointerKey(userId))
                            {
                                ["AccountId"] = new AttributeValue { S = accountId }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                                ["SK"] = new AttributeValue { S = AccountPointerSk },
                                ["CreatedAt"] = new AttributeValue { S = createdAt }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                                ["SK"] = new AttributeValue { S = $"{MemberSkPrefix}{membershipId}" },
                                ["GSI1PK"] = new AttributeValue { S = $"USER#{userId}" },
                                ["GSI1SK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                                ["Email"] = new AttributeValue { S = email },
                                ["Role"] = new AttributeValue { S = TitularRole },
                                ["Status"] = new AttributeValue { S = ActiveStatus },
                                ["UserId"] = new AttributeValue { S = userId },
                                ["CreatedAt"] = new AttributeValue { S = createdAt }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    .. defaultCategoryItems
                ]
            }, cancellationToken);

            return new CreateAccountResult(accountId, AlreadyExisted: false);
        }
        catch (TransactionCanceledException ex)
        {
            // Corrida: outro caller (trigger do Cognito ou outro login concorrente)
            // já criou a Account desse usuário entre o FindAccountIdByUserIdAsync
            // e este Put — o AccountPointer (item 0) é quem barra. Recupera o
            // AccountId do vencedor em vez de propagar erro (criação é idempotente).
            var pointerFailed = ex.CancellationReasons is { Count: > 0 } reasons
                && reasons[0].Code == "ConditionalCheckFailed";

            if (!pointerFailed)
                throw;

            var winnerAccountId = await FindAccountIdByUserIdAsync(userId, cancellationToken);
            if (winnerAccountId is null)
                throw; // não deveria acontecer — se o Put falhou por já existir, GetItem tem que achar

            return new CreateAccountResult(winnerAccountId, AlreadyExisted: true);
        }
    }

    public async Task SetActiveAccountAsync(string userId, string accountId, CancellationToken cancellationToken = default)
    {
        // Troca deliberada de conta ativa (aceitação de convite, FEAT-20) —
        // sobrescrita incondicional ("última operação vence"), diferente de
        // CreateAsync: aqui não há corrida a serializar.
        await _dynamoDbClient.PutItemAsync(new PutItemRequest
        {
            TableName = _options.TableName,
            Item = new Dictionary<string, AttributeValue>(AccountPointerKey(userId))
            {
                ["AccountId"] = new AttributeValue { S = accountId }
            }
        }, cancellationToken);
    }

    private static Dictionary<string, AttributeValue> AccountPointerKey(string userId) => new()
    {
        ["PK"] = new AttributeValue { S = $"USER#{userId}" },
        ["SK"] = new AttributeValue { S = AccountPointerSk }
    };
}
