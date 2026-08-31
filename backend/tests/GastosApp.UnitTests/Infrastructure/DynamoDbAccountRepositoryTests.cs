using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Domain.Categories;
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
    public async Task CreateAsync_ShouldWriteAccountPointerAccountMembershipAndDefaultCategories_WhenNoConflict()
    {
        TransactWriteItemsRequest? captured = null;
        _dynamoDbClientMock.TransactWriteItemsAsync(
                Arg.Do<TransactWriteItemsRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var result = await _repository.CreateAsync("user-1", "user1@email.com");

        result.AlreadyExisted.Should().BeFalse();
        result.AccountId.Should().NotBeNullOrWhiteSpace();

        captured.Should().NotBeNull();
        var items = captured!.TransactItems;

        // 3 itens já existentes (AccountPointer, Account, Membership) + 13
        // categorias padrão (FEAT-28), tudo na mesma transação atômica.
        items.Should().HaveCount(16);

        // Item 0: AccountPointer — único cuja condição realmente serializa a concorrência.
        items[0].Put!.Item["PK"].S.Should().Be("USER#user-1");
        items[0].Put!.Item["SK"].S.Should().Be("ACCOUNT#");
        items[0].Put!.Item["AccountId"].S.Should().Be(result.AccountId);
        items[0].Put!.ConditionExpression.Should().Be("attribute_not_exists(PK)");

        // Item 1: Account
        items[1].Put!.Item["PK"].S.Should().Be($"ACCOUNT#{result.AccountId}");
        items[1].Put!.Item["SK"].S.Should().Be("ACCOUNT#");

        // Item 2: Membership (Titular) — SK usa um Id próprio (FEAT-20), não mais o userId
        items[2].Put!.Item["PK"].S.Should().Be($"ACCOUNT#{result.AccountId}");
        items[2].Put!.Item["SK"].S.Should().StartWith("MEMBER#");
        items[2].Put!.Item["GSI1PK"].S.Should().Be("USER#user-1");
        items[2].Put!.Item["GSI1SK"].S.Should().Be($"ACCOUNT#{result.AccountId}");
        items[2].Put!.Item["Email"].S.Should().Be("user1@email.com");
        items[2].Put!.Item["Role"].S.Should().Be("Titular");
        items[2].Put!.Item["Status"].S.Should().Be("Ativo");
        items[2].Put!.Item["UserId"].S.Should().Be("user-1");

        // Itens 3-15: as 13 categorias padrão (FEAT-28), na mesma ordem de DefaultCategorySeed.Items.
        for (var i = 0; i < DefaultCategorySeed.Items.Count; i++)
        {
            var (id, nome) = DefaultCategorySeed.Items[i];
            var categoryItem = items[3 + i].Put!;

            categoryItem.Item["PK"].S.Should().Be($"ACCOUNT#{result.AccountId}");
            categoryItem.Item["SK"].S.Should().Be($"CAT#{CategorySlug.From(nome)}");
            categoryItem.Item["GSI2PK"].S.Should().Be($"ID#{result.AccountId}#{id}");
            categoryItem.Item["Nome"].S.Should().Be(nome);
            categoryItem.Item["Tipo"].S.Should().Be("categoria");
            categoryItem.Item["TipoLancamento"].S.Should().Be("despesa");
            categoryItem.Item.Should().NotContainKey("OrcamentoMensalCents");
            categoryItem.ConditionExpression.Should().Be("attribute_not_exists(PK)");
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldGenerateDifferentAccountIds_ForDifferentCalls()
    {
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());

        var first = await _repository.CreateAsync("user-1", "user1@email.com");
        var second = await _repository.CreateAsync("user-2", "user2@email.com");

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
        // 16 posições (3 itens já existentes + 13 categorias padrão, FEAT-28) —
        // só o índice 0 (AccountPointer) importa pro tratamento em CreateAsync.
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = Enumerable.Range(0, 16)
                    .Select(i => new CancellationReason { Code = i == 0 ? "ConditionalCheckFailed" : "None" })
                    .ToList()
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
        var result = await _repository.CreateAsync("user-1", "user1@email.com");

        // Assert
        result.AlreadyExisted.Should().BeTrue();
        result.AccountId.Should().Be("account-do-vencedor");
    }

    [Fact]
    public async Task CreateAsync_ShouldRethrow_WhenTransactionCanceledForAnotherReason()
    {
        // Arrange — falha que não é a corrida esperada (ex.: throttling
        // genuíno do DynamoDB) precisa propagar, não ser engolida. 16
        // posições (3 itens já existentes + 13 categorias padrão, FEAT-28);
        // aqui é o índice 1 (Account) que falha, não o 0 (AccountPointer).
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = Enumerable.Range(0, 16)
                    .Select(i => new CancellationReason { Code = i == 1 ? "ConditionalCheckFailed" : "None" })
                    .ToList()
            });

        var act = async () => await _repository.CreateAsync("user-1", "user1@email.com");

        await act.Should().ThrowAsync<TransactionCanceledException>();
    }

    // ----- SetActiveAccountAsync -----

    [Fact]
    public async Task SetActiveAccountAsync_ShouldOverwriteAccountPointer_Unconditionally()
    {
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        await _repository.SetActiveAccountAsync("user-1", "account-convite");

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["PK"].S == "USER#user-1"
                && r.Item["SK"].S == "ACCOUNT#"
                && r.Item["AccountId"].S == "account-convite"
                && r.ConditionExpression == null),
            Arg.Any<CancellationToken>());
    }
}
