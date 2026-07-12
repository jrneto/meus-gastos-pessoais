using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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
}