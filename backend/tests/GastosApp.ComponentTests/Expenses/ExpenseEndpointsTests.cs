using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Common.Cursors;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Expenses;
using NSubstitute;

namespace GastosApp.ComponentTests.Expenses;

public sealed class ExpenseEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExpenseEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetExpenseRepositoryMock();
        _client = factory.CreateClient();
    }

    private void AuthenticateAs(string userId, string email = "neto@email.com", string name = "Neto")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"{userId}|{email}|{name}");
    }

    [Fact]
    public async Task RegisterExpense_ComDadosValidosEUsuarioAutenticado_Retorna201ComLocationEBody()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço no restaurante",
            amountInCents = 4590,
            category = "Alimentacao",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/expenses/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Almoço no restaurante");
        body.GetProperty("amountInCents").GetInt64().Should().Be(4590);
        body.GetProperty("category").GetString().Should().Be("Alimentacao");
        body.GetProperty("expenseDate").GetString().Should().Be("2025-06-15");

        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.UserId == "user-id-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterExpense_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            category = "Alimentacao",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComDescricaoVazia_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "",
            amountInCents = 4590,
            category = "Alimentacao",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComValorMenorOuIgualAZero_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 0,
            category = "Alimentacao",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComCategoriaForaDoEnum_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            category = "CategoriaInexistente",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Theory]
    [InlineData("2020-01-01")] // retroativa
    [InlineData("2999-01-01")] // futura
    public async Task RegisterExpense_ComDataRetroativaOuFutura_Retorna201(string expenseDate)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa",
            amountInCents = 100,
            category = "Outros",
            expenseDate
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegisterExpense_ComDoisUsuariosDiferentes_AssociaCadaDespesaAoUserIdDoRespectivoToken()
    {
        AuthenticateAs("user-id-A");
        await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa do usuário A",
            amountInCents = 100,
            category = "Outros",
            expenseDate = "2025-06-15"
        });

        AuthenticateAs("user-id-B");
        await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa do usuário B",
            amountInCents = 200,
            category = "Outros",
            expenseDate = "2025-06-15"
        });

        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.UserId == "user-id-A" && e.Description == "Despesa do usuário A"),
            Arg.Any<CancellationToken>());
        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.UserId == "user-id-B" && e.Description == "Despesa do usuário B"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterExpense_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");

        _factory.ExpenseRepositoryMock
            .SaveAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("Falha simulada")));

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa",
            amountInCents = 100,
            category = "Outros",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    private static ExpenseQueryPage EmptyPage() => new([], null);

    private static ExpenseQueryItem SampleItem(string id = "expense-1") => new(
        id, "Almoço", 4590, ExpenseCategory.Alimentacao, new DateOnly(2025, 6, 15), DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetExpenses_SemFiltros_Retorna200ComItensPaginados()
    {
        AuthenticateAs("user-id-123");

        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new ExpenseQueryPage([SampleItem()], null));

        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);

        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f =>
                f.UserId == "user-id-123"
                && f.YearMonth == null
                && f.Category == null
                && f.DateFrom == null
                && f.DateTo == null
                && f.MinAmountInCents == null
                && f.MaxAmountInCents == null
                && f.Limit == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComYearMonth_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?yearMonth=2025-06");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.YearMonth == "2025-06"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComCategory_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?category=Alimentacao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.Category == ExpenseCategory.Alimentacao),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComCategoryEYearMonth_RepassaAmbosOsFiltros()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?category=Alimentacao&yearMonth=2025-06");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.Category == ExpenseCategory.Alimentacao && f.YearMonth == "2025-06"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComDateFromEDateTo_RepassaFiltro()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?dateFrom=2025-06-01&dateTo=2025-06-30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f =>
                f.DateFrom == new DateOnly(2025, 6, 1) && f.DateTo == new DateOnly(2025, 6, 30)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComFaixaDeValor_RepassaFiltro()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?minAmountInCents=1000&maxAmountInCents=5000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.MinAmountInCents == 1000 && f.MaxAmountInCents == 5000),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComTodosOsFiltrosCombinados_RepassaTodosAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync(
            "/expenses?category=Alimentacao&yearMonth=2025-06&dateFrom=2025-06-01&dateTo=2025-06-30" +
            "&minAmountInCents=1000&maxAmountInCents=5000&limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f =>
                f.Category == ExpenseCategory.Alimentacao
                && f.YearMonth == "2025-06"
                && f.DateFrom == new DateOnly(2025, 6, 1)
                && f.DateTo == new DateOnly(2025, 6, 30)
                && f.MinAmountInCents == 1000
                && f.MaxAmountInCents == 5000
                && f.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComCursor_UsaCursorInformadoNaProximaChamada()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var cursor = ExpenseCursorCodec.Encode(new ExpenseCursorPayload(
            "Base", new Dictionary<string, string> { ["PK"] = "USER#user-id-123", ["SK"] = "TXN#2025-06-15#abc" }));

        var response = await _client.GetAsync($"/expenses?cursor={cursor}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.Cursor == cursor),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComDoisUsuariosDiferentes_CadaUmVeSomenteSuasDespesas()
    {
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        AuthenticateAs("user-id-A");
        await _client.GetAsync("/expenses");

        AuthenticateAs("user-id-B");
        await _client.GetAsync("/expenses");

        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.UserId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.UserId == "user-id-B"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .QueryAsync(default!, default);
    }

    [Theory]
    [InlineData("dateFrom=2025-06-20&dateTo=2025-06-10")]
    [InlineData("minAmountInCents=5000&maxAmountInCents=1000")]
    [InlineData("yearMonth=2025-13")]
    [InlineData("category=CategoriaInexistente")]
    [InlineData("cursor=not-a-valid-cursor")]
    [InlineData("dateFrom=not-a-date")]
    [InlineData("minAmountInCents=0")]
    public async Task GetExpenses_ComFiltrosInconsistentes_Retorna400SemChamarRepositorio(string queryString)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync($"/expenses?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .QueryAsync(default!, default);
    }

    [Fact]
    public async Task GetExpenses_ComLimitAcimaDoMaximo_Retorna400()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/expenses?limit=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetExpenses_ComLimitAusente_UsaDefaultDeVinte()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        await _client.GetAsync("/expenses");

        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.Limit == 20), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");

        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<ExpenseQueryPage>(new InvalidOperationException("Falha simulada")));

        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }
}