using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Segunda identidade real de teste, criada/convidada/logada por
/// <see cref="TestAccountFixture.InviteAndAcceptAsync"/> — usada pelos
/// cenários que exigem duas contas simultâneas (aceite de convite no
/// login, autorização por autoria do papel Lancar). Tem limpeza própria
/// (Cognito + a conta PESSOAL criada automaticamente no login dela pelo
/// EnsureAccountCommand), independente da limpeza da conta principal que
/// a convidou — ver plan.md, "SecondaryTestAccount — limpeza da segunda
/// conta".
/// </summary>
public sealed class SecondaryTestAccount : IAsyncDisposable
{
    private readonly IntegrationTestEnvironment _env;
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _userPoolId;

    /// <summary>AccountId da conta que convidou esta identidade — nunca apagado por esta limpeza (é responsabilidade da conta principal).</summary>
    private readonly string _invitingAccountId;

    public IApiTransport Transport { get; }
    public string Email { get; }
    public string Cpf { get; }
    public string UserId { get; }
    public string AccessToken { get; }

    internal SecondaryTestAccount(
        IntegrationTestEnvironment env,
        IAmazonCognitoIdentityProvider cognito,
        IAmazonDynamoDB dynamoDb,
        string userPoolId,
        string invitingAccountId,
        IApiTransport transport,
        string email,
        string cpf,
        string userId,
        string accessToken)
    {
        _env = env;
        _cognito = cognito;
        _dynamoDb = dynamoDb;
        _userPoolId = userPoolId;
        _invitingAccountId = invitingAccountId;
        Transport = transport;
        Email = email;
        Cpf = cpf;
        UserId = userId;
        AccessToken = accessToken;
    }

    /// <summary>
    /// Remove tudo que esta identidade criou — a conta PESSOAL dela
    /// (criada pelo EnsureAccountCommand no login embutido em
    /// InviteAndAcceptAsync, mesmo se o convite foi aceito na sequência) e
    /// o próprio usuário no Cognito. NÃO mexe na conta que convidou esta
    /// identidade (<see cref="_invitingAccountId"/>) — a Membership desta
    /// identidade lá dentro já é removida pela limpeza da conta principal
    /// (TestAccountFixture.DisposeAsync, que apaga toda a partição
    /// ACCOUNT#&lt;accountId&gt;, Membership incluída). Roda sempre
    /// (sucesso ou falha do teste), best-effort por etapa — mesmo padrão
    /// de TestAccountFixture.DisposeAsync.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await TryAsync("limpeza da conta pessoal no DynamoDB", CleanupPersonalAccountAsync);
        await TryAsync("exclusão do usuário no Cognito", CleanupCognitoUserAsync);

        Transport.Dispose();
    }

    private async Task CleanupCognitoUserAsync()
    {
        await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _userPoolId,
            Username = Email
        });
    }

    private async Task CleanupPersonalAccountAsync()
    {
        var keysToDelete = new List<Dictionary<string, AttributeValue>>();

        // 1) GSI1PK=USER#<UserId> — todas as Memberships Ativas desta
        //    identidade (a pessoal, Titular, e a que aceitou o convite na
        //    conta principal). Mesmo índice/formato documentado em
        //    backend/docs/data-model.md ("Papel do chamador na conta
        //    ativa"), só sem filtrar GSI1SK — aqui o objetivo é justamente
        //    descobrir as duas contas, não confirmar uma específica.
        var membershipsResponse = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _env.DynamoDbTableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue($"USER#{UserId}")
            }
        });

        var invitingAccountGsi1Sk = $"ACCOUNT#{_invitingAccountId}";
        var personalAccountId = membershipsResponse.Items
            .Where(i => i.TryGetValue("GSI1SK", out var sk) && sk.S != invitingAccountGsi1Sk)
            .Select(i => i["GSI1SK"].S.Replace("ACCOUNT#", string.Empty, StringComparison.Ordinal))
            .FirstOrDefault();

        // 2) ACCOUNT#<contaPessoalId> — Account, Membership (Titular) e as
        //    13 categorias padrão semeadas nela.
        if (personalAccountId is not null)
        {
            var accountItems = await QueryByPartitionKeyAsync($"ACCOUNT#{personalAccountId}");
            keysToDelete.AddRange(accountItems.Select(i => new Dictionary<string, AttributeValue>
            {
                ["PK"] = i["PK"],
                ["SK"] = i["SK"]
            }));
        }

        // 3) USER#<UserId> — AccountPointer + UserProfile.
        var userItems = await QueryByPartitionKeyAsync($"USER#{UserId}");
        keysToDelete.AddRange(userItems.Select(i => new Dictionary<string, AttributeValue>
        {
            ["PK"] = i["PK"],
            ["SK"] = i["SK"]
        }));

        // 4) CpfPointer (unicidade de CPF, FEAT-26)
        keysToDelete.Add(new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue($"CPF#{Cpf}"),
            ["SK"] = new AttributeValue("CPF#")
        });

        await BatchDeleteAsync(keysToDelete);
    }

    private async Task<List<Dictionary<string, AttributeValue>>> QueryByPartitionKeyAsync(string pk)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastEvaluatedKey = null;

        do
        {
            var response = await _dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _env.DynamoDbTableName,
                KeyConditionExpression = "PK = :pk",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = new(pk) },
                ExclusiveStartKey = lastEvaluatedKey
            });

            items.AddRange(response.Items);
            lastEvaluatedKey = response.LastEvaluatedKey?.Count > 0 ? response.LastEvaluatedKey : null;
        } while (lastEvaluatedKey is not null);

        return items;
    }

    private async Task BatchDeleteAsync(List<Dictionary<string, AttributeValue>> keys)
    {
        // BatchWriteItem aceita no máximo 25 requisições por chamada.
        foreach (var chunk in keys.Chunk(25))
        {
            if (chunk.Length == 0)
                continue;

            var request = new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_env.DynamoDbTableName] = chunk
                        .Select(key => new WriteRequest(new DeleteRequest(key)))
                        .ToList()
                }
            };

            await _dynamoDb.BatchWriteItemAsync(request);
        }
    }

    private static async Task TryAsync(string description, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SecondaryTestAccount] Falha na {description}: {ex}");
        }
    }
}
