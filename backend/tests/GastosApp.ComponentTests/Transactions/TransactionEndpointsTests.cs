using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using GastosApp.Domain.Transactions;
using NSubstitute;

namespace GastosApp.ComponentTests.Transactions;

public sealed class TransactionEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private const string CategoryId = "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f";
    private const string IncomeCategoryId = "8a4f0b21-5c3d-4e2b-af90-3d2c4b5e6f70";

    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TransactionEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetTransactionRepositoryMock();
        _factory.ResetCategoryRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _client = factory.CreateClient();
    }

    private void AuthenticateAs(string userId, string email = "neto@email.com", string name = "Neto")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"{userId}|{email}|{name}");
    }

    private void AuthenticateWithRole(string userId, MembershipRole role)
    {
        AuthenticateAs(userId);
        _factory.MembershipRepositoryMock
            .FindByAccountAndUserIdAsync(userId, userId, Arg.Any<CancellationToken>())
            .Returns(Membership.Restore(
                "membership-1", userId, userId, "membro@email.com", role, MembershipStatus.Ativo, DateTimeOffset.UtcNow));
    }

    private void MockOwnedCategory(string accountId, string categoryId, string tipo = "despesa") =>
        _factory.CategoryRepositoryMock.GetByIdAsync(accountId, categoryId, Arg.Any<CancellationToken>())
            .Returns(Category.Restore(categoryId, accountId, "Alimentacao", tipo, null, DateTimeOffset.UtcNow));

    private static Transaction SampleTransaction(
        string accountId = "user-id-123", string id = "transaction-1", string createdByUserId = "user-id-123", string tipo = "despesa") =>
        Transaction.Restore(id, accountId, "Almoço no restaurante", 4590, CategoryId, tipo, new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    // ----- POST /transactions -----

    [Fact]
    public async Task RegisterTransaction_ComDadosValidosDeDespesa_Retorna201ComLocationEBody()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Almoço no restaurante",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/transactions/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Almoço no restaurante");
        body.GetProperty("amountInCents").GetInt64().Should().Be(4590);
        body.GetProperty("categoryId").GetString().Should().Be(CategoryId);
        body.GetProperty("tipo").GetString().Should().Be("despesa");
        body.GetProperty("date").GetString().Should().Be("2025-06-15");
        body.GetProperty("createdByUserId").GetString().Should().Be("user-id-123");
        body.GetProperty("createdByLabel").GetString().Should().Be("Você");

        await _factory.TransactionRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Transaction>(t => t.AccountId == "user-id-123" && t.CreatedByUserId == "user-id-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterTransaction_ComDadosValidosDeReceita_Retorna201()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", IncomeCategoryId, tipo: "receita");

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Salário",
            amountInCents = 500000,
            categoryId = IncomeCategoryId,
            tipo = "receita",
            date = "2025-06-05"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tipo").GetString().Should().Be("receita");
    }

    [Fact]
    public async Task RegisterTransaction_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterTransaction_ComDescricaoVazia_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task RegisterTransaction_ComTipoAusenteOuInvalido_Retorna400SemChamarRepositorio(string tipo)
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo,
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterTransaction_ComTipoDivergenteDaCategoria_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId, tipo: "despesa");

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "receita",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterTransaction_ComCategoriaInexistenteOuDeOutraConta_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-inexistente", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = "category-inexistente",
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Theory]
    [InlineData("2020-01-01")]
    [InlineData("2999-01-01")]
    public async Task RegisterTransaction_ComDataRetroativaOuFutura_Retorna201(string date)
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            description = "Despesa",
            amountInCents = 100,
            categoryId = CategoryId,
            tipo = "despesa",
            date
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ----- GET /transactions -----

    private static TransactionQueryPage EmptyPage() => new([], null);

    private static TransactionQueryItem SampleItem(string id = "transaction-1", string tipo = "despesa", string createdByUserId = "user-id-123") =>
        new(id, "Almoço", 4590, CategoryId, tipo, new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetTransactions_SemFiltros_Retorna200ComTodasAsTransacoes()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([SampleItem()], null));

        var response = await _client.GetAsync("/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(1);

        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-123" && f.Tipo == null && f.Limit == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTransactions_ComTipo_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/transactions?tipo=receita");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.Tipo == "receita"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTransactions_ComTipoInvalido_Retorna400()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/transactions?tipo=invalido");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [Fact]
    public async Task GetTransactions_ComTodosOsFiltrosCombinados_RepassaTodosAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync(
            $"/transactions?tipo=despesa&categoryId={CategoryId}&yearMonth=2025-06&dateFrom=2025-06-01&dateTo=2025-06-30" +
            "&minAmountInCents=1000&maxAmountInCents=5000&limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f =>
                f.Tipo == "despesa"
                && f.CategoryId == CategoryId
                && f.YearMonth == "2025-06"
                && f.DateFrom == new DateOnly(2025, 6, 1)
                && f.DateTo == new DateOnly(2025, 6, 30)
                && f.MinAmountInCents == 1000
                && f.MaxAmountInCents == 5000
                && f.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTransactions_ComCursor_UsaCursorInformadoNaProximaChamada()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var cursor = TransactionCursorCodec.Encode(new TransactionCursorPayload(
            "Base", new Dictionary<string, string> { ["PK"] = "ACCOUNT#user-id-123", ["SK"] = "TXN#2025-06-15#abc" }));

        var response = await _client.GetAsync($"/transactions?cursor={cursor}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.Cursor == cursor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTransactions_ComDoisUsuariosDeContasDiferentes_CadaUmVeSomenteSuasTransacoes()
    {
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        AuthenticateAs("user-id-A");
        await _client.GetAsync("/transactions");

        AuthenticateAs("user-id-B");
        await _client.GetAsync("/transactions");

        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-B"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTransactions_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [Theory]
    [InlineData("dateFrom=2025-06-20&dateTo=2025-06-10")]
    [InlineData("minAmountInCents=5000&maxAmountInCents=1000")]
    [InlineData("yearMonth=2025-13")]
    [InlineData("cursor=not-a-valid-cursor")]
    public async Task GetTransactions_ComFiltrosInconsistentes_Retorna400SemChamarRepositorio(string queryString)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync($"/transactions?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    // ----- GET /transactions/{id} -----

    [Fact]
    public async Task GetTransactionById_QuandoChamadorEOAutor_RetornaCreatedByLabelVoce()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));

        var response = await _client.GetAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be("transaction-1");
        body.GetProperty("createdByLabel").GetString().Should().Be("Você");
    }

    [Fact]
    public async Task GetTransactionById_QuandoLancadaPorOutroMembro_RetornaEmailDoAutorEmCreatedByLabel()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));
        _factory.MembershipRepositoryMock
            .FindByAccountAndUserIdAsync("user-id-123", "outro-membro", Arg.Any<CancellationToken>())
            .Returns(Membership.CreateTitular("user-id-123", "outro-membro", "outro@membro.com"));

        var response = await _client.GetAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("createdByLabel").GetString().Should().Be("outro@membro.com");
    }

    [Fact]
    public async Task GetTransactionById_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetTransactionById_ComTransacaoInexistenteOuDeOutraConta_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var response = await _client.GetAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- PUT /transactions/{id} -----

    [Fact]
    public async Task UpdateTransaction_ComPapelTotalEDadosValidos_Retorna200ComCorpoAtualizadoPreservandoAutor()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Total);
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));

        var updated = Transaction.Restore(
            "transaction-1", "user-id-123", "Almoço atualizado", 5290, CategoryId, "despesa",
            new DateOnly(2025, 6, 16), "user-id-123", DateTimeOffset.UtcNow);

        _factory.TransactionRepositoryMock
            .UpdateAsync("user-id-123", "transaction-1", "Almoço atualizado", 5290, CategoryId, "despesa",
                new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>())
            .Returns(updated);

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço atualizado",
            amountInCents = 5290,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-16"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Almoço atualizado");
        body.GetProperty("date").GetString().Should().Be("2025-06-16");
        body.GetProperty("createdByUserId").GetString().Should().Be("user-id-123");
    }

    [Fact]
    public async Task UpdateTransaction_ComPapelTotalEmTransacaoDeOutroMembro_Retorna200()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Total);
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));
        _factory.TransactionRepositoryMock
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTransaction_ComPapelLancarNaTransacaoQueEleMesmoCriou_Retorna200()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Lancar);
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));
        _factory.TransactionRepositoryMock
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTransaction_ComPapelLancarEmTransacaoDeOutroMembro_Retorna403SemChamarUpdateAsync()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Lancar);
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/insufficient-permission");
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().UpdateAsync(
            default!, default!, default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task UpdateTransaction_ComPapelLeitura_Retorna403SemChamarRepositorio()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Leitura);

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdateTransaction_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().UpdateAsync(
            default!, default!, default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task UpdateTransaction_ComTipoDivergenteDaCategoria_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId, tipo: "despesa");

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "receita",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task UpdateTransaction_ComTransacaoInexistenteOuDeOutraConta_Retorna404()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var response = await _client.PutAsJsonAsync("/transactions/transaction-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            tipo = "despesa",
            date = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- DELETE /transactions/{id} -----

    [Fact]
    public async Task DeleteTransaction_ComPapelTotalEmTransacaoPropria_Retorna204SemCorpo()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Total);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));
        _factory.TransactionRepositoryMock
            .DeleteAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTransaction_ComPapelTotalEmTransacaoDeOutroMembro_Retorna204()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Total);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));
        _factory.TransactionRepositoryMock
            .DeleteAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTransaction_ComPapelLancarNaTransacaoQueEleMesmoCriou_Retorna204()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Lancar);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"));
        _factory.TransactionRepositoryMock
            .DeleteAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTransaction_ComPapelLancarEmTransacaoDeOutroMembro_Retorna403SemChamarDeleteAsync()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Lancar);
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-membro"));

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteTransaction_ComPapelLeitura_Retorna403SemChamarRepositorio()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Leitura);

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteTransaction_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteTransaction_ComTransacaoInexistenteOuDeOutraConta_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var response = await _client.DeleteAsync("/transactions/transaction-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTransaction_ChamadoDuasVezesParaMesmaTransacao_SegundaChamadaRetorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .GetByIdAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-id-123"), (Transaction?)null);
        _factory.TransactionRepositoryMock
            .DeleteAsync("user-id-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var primeiraResposta = await _client.DeleteAsync("/transactions/transaction-1");
        var segundaResposta = await _client.DeleteAsync("/transactions/transaction-1");

        primeiraResposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        segundaResposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----- /expenses não existe mais -----

    [Fact]
    public async Task RotaLegadaExpenses_NaoExisteMais_Retorna404DeRota()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
