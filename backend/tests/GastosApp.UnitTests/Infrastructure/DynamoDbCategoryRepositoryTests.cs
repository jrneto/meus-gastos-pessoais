using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using GastosApp.Domain.Categories;
using GastosApp.Infrastructure.Categories;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class DynamoDbCategoryRepositoryTests
{
    private static readonly DateTimeOffset OriginalCreatedAt = new(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IAmazonDynamoDB _dynamoDbClientMock;
    private readonly DynamoDbCategoryRepository _repository;

    public DynamoDbCategoryRepositoryTests()
    {
        _dynamoDbClientMock = Substitute.For<IAmazonDynamoDB>();
        var options = Options.Create(new DynamoDbOptions { TableName = "GastosApp-unitTests" });
        _repository = new DynamoDbCategoryRepository(_dynamoDbClientMock, options);
    }

    private static Dictionary<string, AttributeValue> BuildItem(
        string accountId, string sk, string id, string nome, string tipoLancamento = "despesa", long? orcamentoMensalCents = null)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            ["SK"] = new AttributeValue { S = sk },
            ["GSI2PK"] = new AttributeValue { S = $"ID#{id}" },
            ["Nome"] = new AttributeValue { S = nome },
            ["TipoLancamento"] = new AttributeValue { S = tipoLancamento },
            ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") }
        };

        if (orcamentoMensalCents is { } valor)
            item["OrcamentoMensalCents"] = new AttributeValue { N = valor.ToString(CultureInfo.InvariantCulture) };

        return item;
    }

    // ----- CreateAsync -----

    [Fact]
    public async Task CreateAsync_ShouldReturnSuccess_WhenPutItemSucceeds()
    {
        // Arrange
        var category = Category.Create("user-1", "Viagem", "despesa", null);
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        var result = await _repository.CreateAsync(category);

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["PK"].S == "ACCOUNT#user-1"
                && r.Item["SK"].S == "CAT#viagem"
                && r.Item["Nome"].S == "Viagem"
                && r.Item["Tipo"].S == "categoria"
                && r.Item["TipoLancamento"].S == "despesa"
                && !r.Item.ContainsKey("OrcamentoMensalCents")
                && r.ConditionExpression == "attribute_not_exists(PK)"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldIncludeOrcamentoMensalCents_WhenInformed()
    {
        // Arrange
        var category = Category.Create("user-1", "Salario", "receita", 500000);
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutItemResponse());

        // Act
        await _repository.CreateAsync(category);

        // Assert
        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["TipoLancamento"].S == "receita"
                && r.Item["OrcamentoMensalCents"].N == "500000"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNameConflict_WhenConditionalCheckFails()
    {
        // Arrange
        var category = Category.Create("user-1", "Lazer", "despesa", null);
        _dynamoDbClientMock.PutItemAsync(Arg.Any<PutItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<PutItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        // Act
        var result = await _repository.CreateAsync(category);

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NameConflict);
    }

    // ----- ListAsync -----

    [Fact]
    public async Task ListAsync_ShouldQueryByPkAndCatPrefix()
    {
        // Arrange
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")]
            });

        // Act
        var result = await _repository.ListAsync("user-1", tipo: null);

        // Assert
        result.Should().ContainSingle();
        result[0].Nome.Should().Be("Viagem");

        await _dynamoDbClientMock.Received(1).QueryAsync(
            Arg.Is<QueryRequest>(r =>
                r.KeyConditionExpression == "PK = :pk AND begins_with(SK, :skPrefix)"
                && r.ExpressionAttributeValues[":pk"].S == "ACCOUNT#user-1"
                && r.ExpressionAttributeValues[":skPrefix"].S == "CAT#"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_ShouldFilterByTipo_AfterMapping()
    {
        // Arrange: um item sem TipoLancamento (categoria antiga, default "despesa" no mapeamento)
        // precisa entrar no filtro de "despesa" — não pode ficar de fora só por faltar o atributo.
        var despesaAntiga = BuildItem("user-1", "CAT#alimentacao", "category-1", "Alimentacao");
        despesaAntiga.Remove("TipoLancamento");
        var despesaNova = BuildItem("user-1", "CAT#transporte", "category-2", "Transporte", "despesa");
        var receita = BuildItem("user-1", "CAT#salario", "category-3", "Salario", "receita");

        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [despesaAntiga, despesaNova, receita] });

        // Act
        var despesas = await _repository.ListAsync("user-1", tipo: "despesa");
        var receitas = await _repository.ListAsync("user-1", tipo: "receita");
        var todas = await _repository.ListAsync("user-1", tipo: null);

        // Assert
        despesas.Should().HaveCount(2);
        despesas.Select(c => c.Nome).Should().BeEquivalentTo("Alimentacao", "Transporte");
        receitas.Should().ContainSingle().Which.Nome.Should().Be("Salario");
        todas.Should().HaveCount(3);
    }

    // ----- GetByIdAsync -----

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.GetByIdAsync("user-1", "category-inexistente");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory_WhenBelongsToUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem", "despesa", 80000)
            });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Viagem");
        result.Tipo.Should().Be("despesa");
        result.OrcamentoMensalCents.Should().Be(80000);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCategory_WhenItemHasNoTipoAttribute()
    {
        // Categorias gravadas antes desta correção nunca tiveram "Tipo" —
        // BuildItem() (helper deste teste) já não inclui, representando esse
        // dado legado; precisa continuar funcionando sem migração.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        var legacyItem = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem");
        legacyItem.Remove("Tipo");
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = true, Item = legacyItem });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldDefaultTipoToDespesa_WhenTipoLancamentoAttributeIsMissing()
    {
        // Categoria gravada antes da FEAT-21 (tipo/orçamento) nunca teve "TipoLancamento" —
        // tratada como "despesa" implícito, mesma postura defensiva já usada pro
        // discriminador "Tipo" acima.
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        var legacyItem = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem");
        legacyItem.Remove("TipoLancamento");
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = true, Item = legacyItem });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().NotBeNull();
        result!.Tipo.Should().Be("despesa");
        result.OrcamentoMensalCents.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldIgnoreCorEIcone_WhenItemStillHasThemFromBeforeThisFeature()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        var legacyItem = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem");
        legacyItem["Cor"] = new AttributeValue { S = "#0EA5E9" };
        legacyItem["Icone"] = new AttributeValue { S = "plane" };
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { IsItemSet = true, Item = legacyItem });

        var result = await _repository.GetByIdAsync("user-1", "category-1");

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Viagem");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenGsi2IdBelongsToAnotherItemType()
    {
        // Mesmo GSI2PK=ID#<id> é compartilhado com Expense — um id de despesa
        // não pode ser confundido com categoria (achado ao revisar
        // backend/docs/data-model.md, mesmo bug já corrigido do lado de Expense).
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#expense-1" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#expense-1" },
                    ["Tipo"] = new AttributeValue { S = "despesa" },
                    ["Description"] = new AttributeValue { S = "Almoço" }
                }
            });

        var result = await _repository.GetByIdAsync("user-1", "expense-1");

        result.Should().BeNull();
    }

    // ----- UpdateAsync -----

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.UpdateAsync("user-1", "category-inexistente", "Viagens", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().GetItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Viagens", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenGsi2IdBelongsToAnotherItemType()
    {
        // Mesmo cenário de GetByIdAsync — sem essa checagem, isto apagaria o
        // item de despesa de verdade (Delete+Put do TransactWriteItems).
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#expense-1" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "TXN#2025-06-15#expense-1" },
                    ["Tipo"] = new AttributeValue { S = "despesa" },
                    ["CreatedAt"] = new AttributeValue { S = OriginalCreatedAt.ToString("O") }
                }
            });

        var result = await _repository.UpdateAsync("user-1", "expense-1", "Viagens", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUsePutItem_WhenSlugIsUnchanged()
    {
        // Arrange: "Viagem" -> "viagem", "Viagem!" também normaliza pra "viagem" (slug igual)
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });

        // Act
        var result = await _repository.UpdateAsync("user-1", "category-1", "Viagem!", "receita", 15000);

        // Assert
        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);
        result.Category!.CreatedAt.Should().Be(OriginalCreatedAt);

        await _dynamoDbClientMock.Received(1).PutItemAsync(
            Arg.Is<PutItemRequest>(r =>
                r.Item["SK"].S == "CAT#viagem"
                && r.Item["TipoLancamento"].S == "receita"
                && r.Item["OrcamentoMensalCents"].N == "15000"
                && !r.Item.ContainsKey("Cor")
                && !r.Item.ContainsKey("Icone")),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().TransactWriteItemsAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUseTransactWriteItems_WhenSlugChanges()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.Success);

        await _dynamoDbClientMock.Received(1).TransactWriteItemsAsync(
            Arg.Is<TransactWriteItemsRequest>(r =>
                r.TransactItems.Count == 2
                && r.TransactItems[0].Delete!.Key["SK"].S == "CAT#viagem"
                && r.TransactItems[0].Delete!.ConditionExpression == "attribute_exists(PK)"
                && r.TransactItems[1].Put!.Item["SK"].S == "CAT#lazer"
                && r.TransactItems[1].Put!.ConditionExpression == "attribute_not_exists(PK)"),
            Arg.Any<CancellationToken>());
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().PutItemAsync(default!, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNameConflict_WhenTransactionCanceledByPutCondition()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "None" },
                    new() { Code = "ConditionalCheckFailed" }
                }
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NameConflict);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenTransactionCanceledByDeleteCondition()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse
            {
                IsItemSet = true,
                Item = BuildItem("user-1", "CAT#viagem", "category-1", "Viagem")
            });
        _dynamoDbClientMock.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<TransactWriteItemsResponse>(_ => throw new TransactionCanceledException("cancelada")
            {
                CancellationReasons = new List<CancellationReason>
                {
                    new() { Code = "ConditionalCheckFailed" },
                    new() { Code = "None" }
                }
            });

        var result = await _repository.UpdateAsync("user-1", "category-1", "Lazer", "despesa", null);

        result.Outcome.Should().Be(GastosApp.Application.Common.Interfaces.CategoryWriteOutcome.NotFound);
    }

    // ----- DeleteAsync -----

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenGsi2QueryFindsNothing()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [] });

        var result = await _repository.DeleteAsync("user-1", "category-inexistente");

        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenCategoryBelongsToAnotherUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#outro-user" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeFalse();
        await _dynamoDbClientMock.DidNotReceiveWithAnyArgs().DeleteItemAsync(default!, default);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenCategoryBelongsToUser()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteItemResponse());

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeTrue();
        await _dynamoDbClientMock.Received(1).DeleteItemAsync(
            Arg.Is<DeleteItemRequest>(r =>
                r.Key["PK"].S == "ACCOUNT#user-1"
                && r.Key["SK"].S == "CAT#viagem"
                && r.ConditionExpression == "attribute_exists(PK) AND (attribute_not_exists(#tipo) OR #tipo = :tipo)"
                && r.ExpressionAttributeNames!["#tipo"] == "Tipo"
                && r.ExpressionAttributeValues![":tipo"].S == "categoria"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenConditionalCheckFails()
    {
        _dynamoDbClientMock.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse
            {
                Items = [new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = "ACCOUNT#user-1" },
                    ["SK"] = new AttributeValue { S = "CAT#viagem" }
                }]
            });
        _dynamoDbClientMock.DeleteItemAsync(Arg.Any<DeleteItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<DeleteItemResponse>(_ => throw new ConditionalCheckFailedException("condição falhou"));

        var result = await _repository.DeleteAsync("user-1", "category-1");

        result.Should().BeFalse();
    }
}
