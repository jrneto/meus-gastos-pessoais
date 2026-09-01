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
    private string? _accountId;

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

        // Resolve o AccountId logo após o login (GetItem direto — mesmo
        // access pattern de AccountPointer descrito em data-model.md, mais
        // barato que a Query por partição já feita em CleanupDynamoDbAsync).
        // Guardado aqui pra ser reaproveitado tanto pela limpeza quanto por
        // InviteAndAcceptAsync (precisa saber qual conta excluir da
        // limpeza da segunda identidade).
        var accountPointerResponse = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _env.DynamoDbTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue($"USER#{UserId}"),
                ["SK"] = new AttributeValue("ACCOUNT#")
            }
        }, cancellationToken);

        _accountId = accountPointerResponse.Item.TryGetValue("AccountId", out var accountIdAttribute)
            ? accountIdAttribute.S
            : throw new InvalidOperationException(
                $"AccountPointer não encontrado para o usuário {UserId} logo após o login — conta não foi resolvida a tempo.");
    }

    /// <summary>
    /// AccountId da conta ativa desta conta de teste, resolvido no setup.
    /// Usado por <see cref="InviteAndAcceptAsync"/> pra saber qual conta
    /// NÃO deve ser apagada na limpeza da segunda identidade convidada.
    /// </summary>
    internal string AccountId => _accountId
        ?? throw new InvalidOperationException("AccountId ainda não resolvido — SetupAsync precisa ter concluído.");

    /// <summary>
    /// Convida um novo e-mail para a conta ativa desta conta (que precisa
    /// ser Titular), registra/confirma/loga uma segunda identidade real
    /// com esse e-mail — o login dispara o aceite automático do convite
    /// (EnsureAccountCommand + AcceptPendingInvitesCommand, FEAT-20),
    /// deixando a Membership dela Ativa na conta desta fixture. Usado
    /// pelos módulos Membros e Transações (autorização por autoria do
    /// papel Lancar). Ver plan.md, "TestAccountFixture.InviteAndAcceptAsync".
    /// </summary>
    public async Task<SecondaryTestAccount> InviteAndAcceptAsync(string role, CancellationToken cancellationToken = default)
    {
        var secondaryEmail = $"int-test+{Guid.NewGuid():N}@jrnexpenses.com";
        var secondaryCpf = CpfGenerator.GenerateUnique();

        // 1) Titular (esta conta) convida o e-mail.
        var inviteResponse = await Transport.SendAsync(
            HttpMethod.Post, "/members",
            new MemberRequestDto(secondaryEmail, role),
            bearerToken: AccessToken, cancellationToken: cancellationToken);

        if (inviteResponse.StatusCode != 201) // 201 Created — ver MemberEndpoints
            throw new InvalidOperationException($"InviteAndAcceptAsync falhou em POST /members ({inviteResponse.StatusCode}): {inviteResponse.Body}");

        // 2) Segunda identidade real — próprio transporte, próprio registro/confirmação.
        var secondaryTransport = ApiTransportFactory.Create(_env);

        var registerResponse = await secondaryTransport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(secondaryEmail, Password, "Membro Convidado (Teste Integrado)", "11988888888", secondaryCpf),
            cancellationToken: cancellationToken);

        if (registerResponse.StatusCode != 201)
            throw new InvalidOperationException($"InviteAndAcceptAsync falhou em POST /auth/register ({registerResponse.StatusCode}): {registerResponse.Body}");

        var secondaryUserId = registerResponse.Deserialize<RegisterResponseDto>().UserId;

        await _cognito.AdminConfirmSignUpAsync(new AdminConfirmSignUpRequest
        {
            UserPoolId = _userPoolId,
            Username = secondaryEmail
        }, cancellationToken);

        // 3) Login da segunda identidade — dispara EnsureAccountCommand
        //    (cria a conta pessoal dela, idempotente) + AcceptPendingInvitesCommand
        //    (aceita o convite do passo 1, troca a conta ativa dela pra
        //    esta conta — a mais recente).
        var loginResponse = await secondaryTransport.SendAsync(
            HttpMethod.Post, "/auth/login",
            new LoginRequestDto(secondaryEmail, Password), cancellationToken: cancellationToken);

        if (loginResponse.StatusCode != 200)
            throw new InvalidOperationException($"InviteAndAcceptAsync falhou em POST /auth/login ({loginResponse.StatusCode}): {loginResponse.Body}");

        var secondaryAccessToken = loginResponse.Deserialize<LoginResponseDto>().AccessToken;

        return new SecondaryTestAccount(
            _env, _cognito, _dynamoDb, _userPoolId!, AccountId,
            secondaryTransport, secondaryEmail, secondaryCpf, secondaryUserId, secondaryAccessToken);
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

        // Reaproveita o AccountId já resolvido em SetupAsync (GetItem logo
        // após o login) em vez de derivá-lo de novo a partir de userItems.
        var accountId = _accountId;

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
