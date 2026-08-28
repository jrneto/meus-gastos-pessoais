using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace GastosApp.IntegrationTests.Support;

/// <summary>
/// Conta de teste dedicada, criada e confirmada no início de uma
/// execução e removida (Cognito + DynamoDB) ao final — mesmo em caso de
/// falha do teste (ver plan.md, "Setup e limpeza da conta de teste").
/// Uso:
/// <code>
/// await using var account = await TestAccountFixture.CreateAsync();
/// // account.AccessToken, account.UserId já disponíveis
/// </code>
/// </summary>
public sealed class TestAccountFixture : IAsyncDisposable
{
    private const string Password = "Teste@Integrado123";

    private readonly IntegrationTestEnvironment _env;
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly IAmazonDynamoDB _dynamoDb;
    private string? _userPoolId;

    public IApiTransport Transport { get; }
    public string Email { get; }
    public string Cpf { get; }
    public string UserId { get; private set; } = default!;
    public string AccessToken { get; private set; } = default!;

    private TestAccountFixture(IntegrationTestEnvironment env)
    {
        _env = env;
        Transport = ApiTransportFactory.Create(env);
        _cognito = AwsClientFactory.CreateCognitoClient(env);
        _dynamoDb = AwsClientFactory.CreateDynamoDbClient(env);

        var unique = Guid.NewGuid().ToString("N");
        Email = $"int-test+{unique}@jrnexpenses.com";
        Cpf = CpfGenerator.GenerateUnique();
    }

    public static async Task<TestAccountFixture> CreateAsync(CancellationToken cancellationToken = default)
    {
        var fixture = new TestAccountFixture(IntegrationTestEnvironment.Current);
        await fixture.SetupAsync(cancellationToken);
        return fixture;
    }

    private async Task SetupAsync(CancellationToken cancellationToken)
    {
        _userPoolId = await ResolveUserPoolIdAsync(cancellationToken);

        var registerResponse = await Transport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(Email, Password, "Conta de Teste Integrado", "11999999999", Cpf),
            cancellationToken: cancellationToken);

        if (registerResponse.StatusCode != 201) // 201 Created — ver AuthEndpoints.RegisterUser
            throw new InvalidOperationException($"Setup falhou em POST /auth/register ({registerResponse.StatusCode}): {registerResponse.Body}");

        var registerBody = registerResponse.Deserialize<RegisterResponseDto>();
        UserId = registerBody.UserId;

        // Confirma sem depender de e-mail real — cognito-local expõe a
        // mesma API HTTP/JSON do Cognito real, então esta chamada
        // funciona igual nos três ambientes (ver plan.md).
        await _cognito.AdminConfirmSignUpAsync(new AdminConfirmSignUpRequest
        {
            UserPoolId = _userPoolId,
            Username = Email
        }, cancellationToken);

        var loginResponse = await Transport.SendAsync(
            HttpMethod.Post, "/auth/login",
            new LoginRequestDto(Email, Password),
            cancellationToken: cancellationToken);

        if (loginResponse.StatusCode != 200) // 200 OK — ver AuthEndpoints.Login
            throw new InvalidOperationException($"Setup falhou em POST /auth/login ({loginResponse.StatusCode}): {loginResponse.Body}");

        AccessToken = loginResponse.Deserialize<LoginResponseDto>().AccessToken;
    }

    private async Task<string> ResolveUserPoolIdAsync(CancellationToken cancellationToken)
    {
        using var ssm = AwsClientFactory.CreateSsmClient(_env);

        // Mesmo padrão de paginação de AwsParameterStoreExtensions
        // (Infrastructure) — GetParametersByPath pagina em lotes de até
        // 10 por padrão.
        string? nextToken = null;
        do
        {
            var response = await ssm.GetParametersByPathAsync(new GetParametersByPathRequest
            {
                Path = _env.ParameterStorePath,
                Recursive = true,
                WithDecryption = true,
                NextToken = nextToken
            }, cancellationToken);

            var parameter = response.Parameters.FirstOrDefault(p => p.Name.EndsWith("Cognito/UserPoolId", StringComparison.Ordinal));
            if (parameter is not null)
                return parameter.Value;

            nextToken = response.NextToken;
        } while (!string.IsNullOrEmpty(nextToken));

        throw new InvalidOperationException(
            $"Parâmetro 'Cognito/UserPoolId' não encontrado sob '{_env.ParameterStorePath}' " +
            $"(modo {_env.Mode}) — confirme se o Parameter Store do ambiente está seedado.");
    }

    /// <summary>
    /// Remove tudo que esta conta criou — roda sempre (sucesso ou falha
    /// do teste). Cada etapa é best-effort (não interrompe a limpeza das
    /// demais em caso de falha isolada), mas reporta o que não conseguiu
    /// limpar via stderr, pra não deixar rastro silencioso em hom/prod.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await TryAsync("limpeza de itens no DynamoDB", CleanupDynamoDbAsync);
        await TryAsync("exclusão do usuário no Cognito", CleanupCognitoUserAsync);

        Transport.Dispose();
        _cognito.Dispose();
        _dynamoDb.Dispose();
    }

    private async Task CleanupCognitoUserAsync()
    {
        await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _userPoolId,
            Username = Email
        });
    }

    private async Task CleanupDynamoDbAsync()
    {
        var keysToDelete = new List<Dictionary<string, AttributeValue>>();

        // 1) USER#<userId> → AccountPointer (SK=ACCOUNT#) + UserProfile (SK=PROFILE#)
        var userItems = await QueryByPartitionKeyAsync($"USER#{UserId}");
        keysToDelete.AddRange(userItems.Select(i => new Dictionary<string, AttributeValue>
        {
            ["PK"] = i["PK"],
            ["SK"] = i["SK"]
        }));

        var accountPointer = userItems.FirstOrDefault(i => i["SK"].S == "ACCOUNT#");
        var accountId = accountPointer?["AccountId"].S;

        // 2) ACCOUNT#<accountId> → Account, Membership(s), categorias padrão
        //    e qualquer Category/Transaction que o teste tenha criado.
        if (accountId is not null)
        {
            var accountItems = await QueryByPartitionKeyAsync($"ACCOUNT#{accountId}");
            keysToDelete.AddRange(accountItems.Select(i => new Dictionary<string, AttributeValue>
            {
                ["PK"] = i["PK"],
                ["SK"] = i["SK"]
            }));
        }

        // 3) CpfPointer (unicidade de CPF, FEAT-26)
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
            Console.Error.WriteLine($"[TestAccountFixture] Falha na {description}: {ex}");
        }
    }
}
