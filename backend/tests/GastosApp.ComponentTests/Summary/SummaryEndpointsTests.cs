using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.ComponentTests.Summary;

public sealed class SummaryEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SummaryEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetTransactionRepositoryMock();
        _factory.ResetCategoryRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _client = factory.CreateClient();

        // Default: nenhuma categoria com orçamento — cada teste que precisar
        // de "porCategoria"/"orcamentoTotalCents" sobrescreve explicitamente.
        _factory.CategoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
    }

    private void AuthenticateAs(string userId, string email = "neto@email.com", string name = "Neto") =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"{userId}|{email}|{name}");

    private void AuthenticateWithRole(string userId, MembershipRole role)
    {
        AuthenticateAs(userId);
        _factory.MembershipRepositoryMock
            .FindByAccountAndUserIdAsync(userId, userId, Arg.Any<CancellationToken>())
            .Returns(Membership.Restore(
                "membership-1", userId, userId, "membro@email.com", role, MembershipStatus.Ativo, DateTimeOffset.UtcNow));
    }

    private static TransactionQueryItem Item(
        string id, long amountInCents, string tipo, string categoryId = "cat-1", string createdByUserId = "user-id-123") =>
        new(id, "Descrição", amountInCents, categoryId, tipo, new DateOnly(2026, 8, 15), createdByUserId, DateTimeOffset.UtcNow);

    private static Category BudgetedCategory(string id, string nome, long orcamentoMensalCents, string accountId = "user-id-123") =>
        Category.Restore(id, accountId, nome, "despesa", orcamentoMensalCents, DateTimeOffset.UtcNow);

    // ----- Resumo com dados -----

    [Fact]
    public async Task GetSummary_ComDados_Retorna200ComNumerosCalculados()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage(
                [
                    Item("t1", 520000, "receita"),
                    Item("t2", 30670, "despesa", categoryId: "cat-alimentacao"),
                    Item("t3", 94610, "despesa", categoryId: "cat-outros")
                ],
                null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-alimentacao", "Alimentacao", 80000) });

        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be("2026-08");
        body.GetProperty("receitasCents").GetInt64().Should().Be(520000);
        body.GetProperty("gastoCents").GetInt64().Should().Be(125280);
        body.GetProperty("saldoCents").GetInt64().Should().Be(394720);
        body.GetProperty("orcamentoTotalCents").GetInt64().Should().Be(80000);
        body.GetProperty("restanteCents").GetInt64().Should().Be(-45280);
    }

    [Fact]
    public async Task GetSummary_SemMonth_Retorna400()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/summary");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026/08")]
    [InlineData("agosto-2026")]
    public async Task GetSummary_ComMonthEmFormatoInvalido_Retorna400(string month)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync($"/summary?month={month}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    // ----- Mês sem dados -----

    [Fact]
    public async Task GetSummary_ComMesSemTransacoes_Retorna200ComTudoZerado()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        var response = await _client.GetAsync("/summary?month=2026-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("saldoCents").GetInt64().Should().Be(0);
        body.GetProperty("receitasCents").GetInt64().Should().Be(0);
        body.GetProperty("gastoCents").GetInt64().Should().Be(0);
        body.GetProperty("ultimosLancamentos").GetArrayLength().Should().Be(0);
    }

    // ----- Por categoria -----

    [Fact]
    public async Task GetSummary_PorCategoria_SoComDespesaEOrcamentoDefinido_OrdenadaPorGastoDecrescente()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage(
                [
                    Item("t1", 30670, "despesa", categoryId: "cat-com-orcamento"),
                    Item("t2", 94610, "despesa", categoryId: "cat-mais-gasto")
                ],
                null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                BudgetedCategory("cat-com-orcamento", "Alimentacao", 80000),
                BudgetedCategory("cat-mais-gasto", "Transporte", 100000),
                Category.Restore("cat-sem-orcamento", "user-id-123", "Lazer", "despesa", null, DateTimeOffset.UtcNow)
            });

        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var porCategoria = body.GetProperty("porCategoria");
        porCategoria.GetArrayLength().Should().Be(2);
        porCategoria[0].GetProperty("categoryId").GetString().Should().Be("cat-mais-gasto");
        porCategoria[1].GetProperty("categoryId").GetString().Should().Be("cat-com-orcamento");
    }

    // ----- Últimos lançamentos -----

    [Fact]
    public async Task GetSummary_UltimosLancamentos_LimitadoA5EOrdenado()
    {
        AuthenticateAs("user-id-123");
        var items = Enumerable.Range(1, 7).Select(i => Item($"t{i}", 1000 * i, "despesa")).ToList();
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage(items, null));

        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ultimos = body.GetProperty("ultimosLancamentos");
        ultimos.GetArrayLength().Should().Be(5);
        ultimos[0].GetProperty("id").GetString().Should().Be("t1");
        ultimos[4].GetProperty("id").GetString().Should().Be("t5");
    }

    // ----- Restante negativo -----

    [Fact]
    public async Task GetSummary_RestanteNegativo_QuandoGastoUltrapassaOrcamentoTotal()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item("t1", 200000, "despesa")], null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-1", "Alimentacao", 80000) });

        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("restanteCents").GetInt64().Should().Be(-120000);
    }

    // ----- Isolamento entre contas -----

    [Fact]
    public async Task GetSummary_ComDoisUsuariosDeContasDiferentes_CadaUmVeSomenteSeuResumo()
    {
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        AuthenticateAs("user-id-A");
        await _client.GetAsync("/summary?month=2026-08");

        AuthenticateAs("user-id-B");
        await _client.GetAsync("/summary?month=2026-08");

        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-B"), Arg.Any<CancellationToken>());
    }

    // ----- Qualquer papel autenticado -----

    [Theory]
    [InlineData(MembershipRole.Leitura)]
    [InlineData(MembershipRole.Lancar)]
    [InlineData(MembershipRole.Total)]
    [InlineData(MembershipRole.Titular)]
    public async Task GetSummary_ComQualquerPapel_Retorna200(MembershipRole role)
    {
        AuthenticateWithRole("user-id-123", role);
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ----- 401 -----

    [Fact]
    public async Task GetSummary_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/summary?month=2026-08");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }
}
