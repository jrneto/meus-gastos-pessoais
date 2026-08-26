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

namespace GastosApp.ComponentTests.Reports;

public sealed class ReportEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ReportEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetTransactionRepositoryMock();
        _factory.ResetCategoryRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _client = factory.CreateClient();

        _factory.CategoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
        _factory.TransactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));
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
        string id, long amountInCents, string categoryId = "cat-1", string createdByUserId = "user-id-123") =>
        new(id, "Descrição", amountInCents, categoryId, "despesa", new DateOnly(2026, 8, 15), createdByUserId, DateTimeOffset.UtcNow);

    private static Category BudgetedCategory(string id, string nome, long orcamentoMensalCents, string accountId = "user-id-123") =>
        Category.Restore(id, accountId, nome, "despesa", orcamentoMensalCents, DateTimeOffset.UtcNow);

    // Configura a resposta da query cujo DateFrom bate com o início esperado
    // (o Handler faz duas chamadas a QueryAsync por request: período atual e anterior).
    private void SetupQueryForPeriodStarting(DateOnly start, TransactionQueryPage page) =>
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Is<TransactionQueryFilter>(f => f.DateFrom == start), Arg.Any<CancellationToken>())
            .Returns(page);

    // ----- Relatório mensal com dados (US1) -----

    [Fact]
    public async Task GetReports_ComDadosMensais_Retorna200ComNumerosCalculados()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage(
            [
                Item("t1", 43510, "cat-alimentacao"),
                Item("t2", 31020, "cat-moradia")
            ],
            null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-alimentacao", "Alimentacao", 80000) });

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("period").GetString().Should().Be("month");
        body.GetProperty("startDate").GetString().Should().Be("2026-08-01");
        body.GetProperty("endDate").GetString().Should().Be("2026-08-31");
        body.GetProperty("totalCents").GetInt64().Should().Be(74530);
        var maiorGasto = body.GetProperty("maiorGasto");
        maiorGasto.GetProperty("categoryId").GetString().Should().Be("cat-alimentacao");
        maiorGasto.GetProperty("percentualOrcamento").GetDecimal().Should().Be(54.4m);
    }

    // ----- Relatório semanal (US2) -----

    [Fact]
    public async Task GetReports_PeriodoSemanal_UsaSemanaIso()
    {
        AuthenticateAs("user-id-123");

        // 2026-08-19 é uma quarta-feira; a semana ISO vai de 2026-08-17 a 2026-08-23.
        var response = await _client.GetAsync("/reports?period=week&date=2026-08-19");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("startDate").GetString().Should().Be("2026-08-17");
        body.GetProperty("endDate").GetString().Should().Be("2026-08-23");
    }

    // ----- Relatório anual (US3) -----

    [Fact]
    public async Task GetReports_PeriodoAnual_UsaAnoCalendario()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/reports?period=year&date=2026-08-15");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("startDate").GetString().Should().Be("2026-01-01");
        body.GetProperty("endDate").GetString().Should().Be("2026-12-31");
    }

    // ----- period ausente/inválido (US4) -----

    [Theory]
    [InlineData("")]
    [InlineData("dia")]
    public async Task GetReports_ComPeriodAusenteOuInvalido_Retorna400(string period)
    {
        AuthenticateAs("user-id-123");

        var url = period == "" ? "/reports?date=2026-08-15" : $"/reports?period={period}&date=2026-08-15";
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    // ----- date ausente/inválida (US5) -----

    [Theory]
    [InlineData("")]
    [InlineData("2026-02-30")]
    [InlineData("agosto")]
    public async Task GetReports_ComDateAusenteOuInvalida_Retorna400(string date)
    {
        AuthenticateAs("user-id-123");

        var url = date == "" ? "/reports?period=month" : $"/reports?period=month&date={date}";
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/validation-error");
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    // ----- Variação percentual (US6/US7/US8) -----

    [Fact]
    public async Task GetReports_VariacaoPositiva_QuandoTotalAtualMaiorQueAnterior()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 138120)], null));
        SetupQueryForPeriodStarting(new DateOnly(2026, 7, 1), new TransactionQueryPage([Item("t2", 123321)], null));

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("variacaoPercentual").GetDecimal().Should().BeApproximately(12.0m, 0.01m);
    }

    [Fact]
    public async Task GetReports_VariacaoNegativa_QuandoTotalAtualMenorQueAnterior()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 50000)], null));
        SetupQueryForPeriodStarting(new DateOnly(2026, 7, 1), new TransactionQueryPage([Item("t2", 100000)], null));

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("variacaoPercentual").GetDecimal().Should().Be(-50.0m);
    }

    [Fact]
    public async Task GetReports_VariacaoNula_QuandoPeriodoAnteriorSemGastoEAtualComGasto()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 50000)], null));

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("variacaoPercentual").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ----- Ambos os períodos sem despesa (US9) -----

    [Fact]
    public async Task GetReports_ComAmbosPeriodosSemDespesa_Retorna200ComTudoZerado()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalCents").GetInt64().Should().Be(0);
        body.GetProperty("variacaoPercentual").GetDecimal().Should().Be(0m);
        body.GetProperty("porCategoria").GetArrayLength().Should().Be(0);
        body.GetProperty("maiorGasto").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ----- Maior gasto (US10/US11) -----

    [Fact]
    public async Task GetReports_MaiorGasto_ComOrcamentoDefinido_RetornaPercentualOrcamento()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 43510, "cat-1")], null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-1", "Alimentacao", 80000) });

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("maiorGasto").GetProperty("percentualOrcamento").GetDecimal().Should().Be(54.4m);
    }

    [Fact]
    public async Task GetReports_MaiorGasto_SemOrcamentoDefinido_RetornaPercentualOrcamentoNulo()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 43510, "cat-1")], null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { Category.Restore("cat-1", "user-id-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow) });

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var maiorGasto = body.GetProperty("maiorGasto");
        maiorGasto.GetProperty("categoryId").GetString().Should().Be("cat-1");
        maiorGasto.GetProperty("percentualOrcamento").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ----- Gasto por categoria ordenado, sem categorias zeradas (US12) -----

    [Fact]
    public async Task GetReports_PorCategoria_OrdenadaDecrescente_SemCategoriasZeradas()
    {
        AuthenticateAs("user-id-123");
        SetupQueryForPeriodStarting(new DateOnly(2026, 8, 1), new TransactionQueryPage(
            [
                Item("t1", 30670, "cat-menos-gasto"),
                Item("t2", 94610, "cat-mais-gasto")
            ],
            null));
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                BudgetedCategory("cat-menos-gasto", "Alimentacao", 80000),
                BudgetedCategory("cat-mais-gasto", "Transporte", 100000),
                Category.Restore("cat-sem-gasto", "user-id-123", "Lazer", "despesa", null, DateTimeOffset.UtcNow)
            });

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var porCategoria = body.GetProperty("porCategoria");
        porCategoria.GetArrayLength().Should().Be(2);
        porCategoria[0].GetProperty("categoryId").GetString().Should().Be("cat-mais-gasto");
        porCategoria[1].GetProperty("categoryId").GetString().Should().Be("cat-menos-gasto");
    }

    // ----- Isolamento entre contas (US13) -----

    [Fact]
    public async Task GetReports_ComDoisUsuariosDeContasDiferentes_CadaUmVeSomenteSeuRelatorio()
    {
        AuthenticateAs("user-id-A");
        await _client.GetAsync("/reports?period=month&date=2026-08-15");

        AuthenticateAs("user-id-B");
        await _client.GetAsync("/reports?period=month&date=2026-08-15");

        await _factory.TransactionRepositoryMock.Received(2).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.TransactionRepositoryMock.Received(2).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-B"), Arg.Any<CancellationToken>());
    }

    // ----- Qualquer papel autenticado (US14) -----

    [Theory]
    [InlineData(MembershipRole.Leitura)]
    [InlineData(MembershipRole.Lancar)]
    [InlineData(MembershipRole.Total)]
    [InlineData(MembershipRole.Titular)]
    public async Task GetReports_ComQualquerPapel_Retorna200(MembershipRole role)
    {
        AuthenticateWithRole("user-id-123", role);

        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ----- 401 -----

    [Fact]
    public async Task GetReports_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/reports?period=month&date=2026-08-15");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }
}
