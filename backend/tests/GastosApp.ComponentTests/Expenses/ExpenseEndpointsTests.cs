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
using GastosApp.Domain.Expenses;
using NSubstitute;

namespace GastosApp.ComponentTests.Expenses;

public sealed class ExpenseEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private const string CategoryId = "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f";

    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExpenseEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetExpenseRepositoryMock();
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

    private void MockOwnedCategory(string userId, string categoryId) =>
        _factory.CategoryRepositoryMock.GetByIdAsync(userId, categoryId, Arg.Any<CancellationToken>())
            .Returns(Category.Restore(categoryId, userId, "Alimentacao", "despesa", null, DateTimeOffset.UtcNow));

    [Fact]
    public async Task RegisterExpense_ComDadosValidosEUsuarioAutenticado_Retorna201ComLocationEBody()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço no restaurante",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/expenses/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Almoço no restaurante");
        body.GetProperty("amountInCents").GetInt64().Should().Be(4590);
        body.GetProperty("categoryId").GetString().Should().Be(CategoryId);
        body.GetProperty("expenseDate").GetString().Should().Be("2025-06-15");

        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.AccountId == "user-id-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterExpense_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
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
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "",
            amountInCents = 4590,
            categoryId = CategoryId,
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
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 0,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComCategoriaInexistente_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-inexistente", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = "category-inexistente",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComCategoriaDeOutroUsuario_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-A");
        // Categoria existe, mas pertence a outro usuário — GetByIdAsync (já filtra por posse) retorna null pra este userId.
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-A", CategoryId, Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
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
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa",
            amountInCents = 100,
            categoryId = CategoryId,
            expenseDate
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RegisterExpense_ComDoisUsuariosDiferentes_AssociaCadaDespesaAoUserIdDoRespectivoToken()
    {
        AuthenticateAs("user-id-A");
        MockOwnedCategory("user-id-A", CategoryId);
        await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa do usuário A",
            amountInCents = 100,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        AuthenticateAs("user-id-B");
        MockOwnedCategory("user-id-B", CategoryId);
        await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa do usuário B",
            amountInCents = 200,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.AccountId == "user-id-A" && e.Description == "Despesa do usuário A"),
            Arg.Any<CancellationToken>());
        await _factory.ExpenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.AccountId == "user-id-B" && e.Description == "Despesa do usuário B"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterExpense_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        _factory.ExpenseRepositoryMock
            .SaveAsync(Arg.Any<Expense>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("Falha simulada")));

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Despesa",
            amountInCents = 100,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    private static ExpenseQueryPage EmptyPage() => new([], null);

    private static ExpenseQueryItem SampleItem(string id = "expense-1") => new(
        id, "Almoço", 4590, CategoryId, new DateOnly(2025, 6, 15), DateTimeOffset.UtcNow);

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
                f.AccountId == "user-id-123"
                && f.YearMonth == null
                && f.CategoryId == null
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
    public async Task GetExpenses_ComCategoryId_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync($"/expenses?categoryId={CategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.CategoryId == CategoryId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetExpenses_ComCategoryIdSemDespesasCorrespondentes_Retorna200ComListaVazia()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses?categoryId=categoria-sem-despesas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetExpenses_ComCategoryIdEYearMonth_RepassaAmbosOsFiltros()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync($"/expenses?categoryId={CategoryId}&yearMonth=2025-06");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.CategoryId == CategoryId && f.YearMonth == "2025-06"),
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
            $"/expenses?categoryId={CategoryId}&yearMonth=2025-06&dateFrom=2025-06-01&dateTo=2025-06-30" +
            "&minAmountInCents=1000&maxAmountInCents=5000&limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f =>
                f.CategoryId == CategoryId
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
            "Base", new Dictionary<string, string> { ["PK"] = "ACCOUNT#user-id-123", ["SK"] = "TXN#2025-06-15#abc" }));

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
            Arg.Is<ExpenseQueryFilter>(f => f.AccountId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.ExpenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.AccountId == "user-id-B"), Arg.Any<CancellationToken>());
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

    // FEAT-19: ResolveAccountEndpointFilter roda antes de qualquer handler —
    // usuário autenticado sem Account resolvível (situação que só ocorreria
    // por dado corrompido/manual) nunca chega no ExpenseRepositoryMock.
    [Fact]
    public async Task GetExpenses_ComUsuarioSemContaResolvivel_Retorna401ComAccountNotFound()
    {
        AuthenticateAs("user-sem-conta");
        _factory.AccountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/account-not-found");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [Theory]
    [InlineData("dateFrom=2025-06-20&dateTo=2025-06-10")]
    [InlineData("minAmountInCents=5000&maxAmountInCents=1000")]
    [InlineData("yearMonth=2025-13")]
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

    [Fact]
    public async Task DeleteExpense_ComDespesaPropria_Retorna204SemCorpo()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .DeleteAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();

        await _factory.ExpenseRepositoryMock.Received(1).DeleteAsync(
            "user-id-123", "expense-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteExpense_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.DeleteAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteExpense_ComDespesaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .DeleteAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(false);

        var response = await _client.DeleteAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task DeleteExpense_ChamadoDuasVezesParaMesmaDespesa_SegundaChamadaRetorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .DeleteAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(true, false);

        var primeiraResposta = await _client.DeleteAsync("/expenses/expense-1");
        var segundaResposta = await _client.DeleteAsync("/expenses/expense-1");

        primeiraResposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        segundaResposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteExpense_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");

        _factory.ExpenseRepositoryMock
            .DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<bool>(new InvalidOperationException("Falha simulada")));

        var response = await _client.DeleteAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    [Fact]
    public async Task GetExpenseById_ComDespesaPropria_Retorna200ComCorpo()
    {
        AuthenticateAs("user-id-123");
        var expense = Expense.Restore(
            "expense-1", "user-id-123", "Almoço no restaurante", 4590,
            CategoryId, new DateOnly(2025, 6, 15), DateTimeOffset.UtcNow);

        _factory.ExpenseRepositoryMock
            .GetByIdAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(expense);

        var response = await _client.GetAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be("expense-1");
        body.GetProperty("description").GetString().Should().Be("Almoço no restaurante");
        body.GetProperty("amountInCents").GetInt64().Should().Be(4590);
        body.GetProperty("categoryId").GetString().Should().Be(CategoryId);
        body.GetProperty("expenseDate").GetString().Should().Be("2025-06-15");
    }

    [Fact]
    public async Task GetExpenseById_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetExpenseById_ComDespesaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.ExpenseRepositoryMock
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        var response = await _client.GetAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task GetExpenseById_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");

        _factory.ExpenseRepositoryMock
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Expense?>(new InvalidOperationException("Falha simulada")));

        var response = await _client.GetAsync("/expenses/expense-1");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    [Fact]
    public async Task UpdateExpense_ComDespesaPropriaEDadosValidos_Retorna200ComCorpoAtualizado()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);
        var updatedExpense = Expense.Restore(
            "expense-1", "user-id-123", "Almoço atualizado", 5290,
            CategoryId, new DateOnly(2025, 6, 16), DateTimeOffset.UtcNow);

        _factory.ExpenseRepositoryMock
            .UpdateAsync("user-id-123", "expense-1", "Almoço atualizado", 5290, CategoryId,
                new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>())
            .Returns(updatedExpense);

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço atualizado",
            amountInCents = 5290,
            categoryId = CategoryId,
            expenseDate = "2025-06-16"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("description").GetString().Should().Be("Almoço atualizado");
        body.GetProperty("amountInCents").GetInt64().Should().Be(5290);
        body.GetProperty("expenseDate").GetString().Should().Be("2025-06-16");
    }

    [Fact]
    public async Task UpdateExpense_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/unauthorized");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default, default!, default, default);
    }

    [Fact]
    public async Task UpdateExpense_ComDescricaoVazia_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default, default!, default, default);
    }

    [Fact]
    public async Task UpdateExpense_ComValorMenorOuIgualAZero_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 0,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default, default!, default, default);
    }

    [Fact]
    public async Task UpdateExpense_ComCategoriaInexistente_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-inexistente", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = "category-inexistente",
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default, default!, default, default);
    }

    [Fact]
    public async Task UpdateExpense_ComDespesaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);
        _factory.ExpenseRepositoryMock
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(),
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task UpdateExpense_QuandoRepositorioLancaExcecaoNaoPrevista_Retorna500()
    {
        AuthenticateAs("user-id-123");
        MockOwnedCategory("user-id-123", CategoryId);

        _factory.ExpenseRepositoryMock
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(),
                Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<Expense?>(new InvalidOperationException("Falha simulada")));

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    // ----- Autorização por papel (FEAT-20) -----

    [Fact]
    public async Task RegisterExpense_ComPapelLeitura_Retorna403SemChamarRepositorio()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Leitura);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/insufficient-permission");

        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task RegisterExpense_ComPapelLancar_Retorna201()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Lancar);
        MockOwnedCategory("user-id-123", CategoryId);

        var response = await _client.PostAsJsonAsync("/expenses", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    public async Task UpdateExpense_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-id-123", Enum.Parse<MembershipRole>(role));

        var response = await _client.PutAsJsonAsync("/expenses/expense-1", new
        {
            description = "Almoço",
            amountInCents = 4590,
            categoryId = CategoryId,
            expenseDate = "2025-06-15"
        });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs().UpdateAsync(
            default!, default!, default!, default, default!, default, default);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    public async Task DeleteExpense_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-id-123", Enum.Parse<MembershipRole>(role));

        var response = await _client.DeleteAsync("/expenses/expense-1");

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.ExpenseRepositoryMock.DidNotReceiveWithAnyArgs()
            .DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetExpenses_ComPapelLeitura_Retorna200()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Leitura);
        _factory.ExpenseRepositoryMock
            .QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage());

        var response = await _client.GetAsync("/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
