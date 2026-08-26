using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.ComponentTests.Transactions;

public sealed class ExportTransactionsEndpointTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private const string CategoryId = "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f";

    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExportTransactionsEndpointTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetTransactionRepositoryMock();
        _factory.ResetCategoryRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _client = factory.CreateClient();

        _factory.CategoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
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
        string id = "transaction-1",
        string description = "Almoço no restaurante",
        long amountInCents = 4590,
        string categoryId = CategoryId,
        string tipo = "despesa",
        string createdByUserId = "user-id-123") =>
        new(id, description, amountInCents, categoryId, tipo, new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    private static async Task<string> ReadCsvAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(bytes.Skip(3).ToArray()); // pula o BOM
    }

    [Fact]
    public async Task ExportTransactions_SemFiltro_Retorna200ComCsvDeTodasAsTransacoes()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item()], null));
        _factory.CategoryRepositoryMock
            .ListAsync("user-id-123", null, Arg.Any<CancellationToken>())
            .Returns(new List<Category> { Category.Restore(CategoryId, "user-id-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow) });

        var response = await _client.GetAsync("/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = await ReadCsvAsync(response);
        csv.Should().Be(
            "data;descricao;categoria;tipo;valor;lancadoPor\r\n" +
            "2025-06-15;Almoço no restaurante;Alimentacao;despesa;45,90;Você\r\n");
    }

    [Fact]
    public async Task ExportTransactions_ComContentTypeEContentDisposition_RetornaHeadersDeArquivo()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/transactions/export");

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentType!.CharSet.Should().Be("utf-8");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("transacoes.csv");
    }

    [Fact]
    public async Task ExportTransactions_ComTipo_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/transactions/export?tipo=receita");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.Tipo == "receita" && f.Cursor == null && f.Limit == int.MaxValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportTransactions_ComCategoryId_ResolveNomeDaCategoriaNaColuna()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(categoryId: CategoryId)], null));
        _factory.CategoryRepositoryMock
            .ListAsync("user-id-123", null, Arg.Any<CancellationToken>())
            .Returns(new List<Category> { Category.Restore(CategoryId, "user-id-123", "Transporte", "despesa", null, DateTimeOffset.UtcNow) });

        var response = await _client.GetAsync($"/transactions/export?categoryId={CategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCsvAsync(response)).Should().Contain(";Transporte;");
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.CategoryId == CategoryId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportTransactions_ComYearMonth_RepassaFiltroAoRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/transactions/export?yearMonth=2025-06");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.YearMonth == "2025-06"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportTransactions_SemNenhumaTransacaoCorrespondente_Retorna200ComCsvSoDeCabecalho()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadCsvAsync(response)).Should().Be("data;descricao;categoria;tipo;valor;lancadoPor\r\n");
    }

    [Theory]
    [InlineData("tipo=invalido")]
    [InlineData("dateFrom=2025-06-20&dateTo=2025-06-10")]
    [InlineData("minAmountInCents=5000&maxAmountInCents=1000")]
    [InlineData("yearMonth=2025-13")]
    public async Task ExportTransactions_ComFiltroInvalido_Retorna400SemChamarRepositorio(string queryString)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync($"/transactions/export?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [Fact]
    public async Task ExportTransactions_ComValorEmCentavos_FormataColunaValorEmReaisComVirgula()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(amountInCents: 500000)], null));

        var response = await _client.GetAsync("/transactions/export");

        (await ReadCsvAsync(response)).Should().Contain(";5000,00;");
    }

    [Fact]
    public async Task ExportTransactions_ComDescricaoContendoDelimitadorEAspas_EscapaConformeRfc4180()
    {
        AuthenticateAs("user-id-123");
        _factory.TransactionRepositoryMock
            .QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(description: "Almoço; sobremesa \"extra\"")], null));

        var response = await _client.GetAsync("/transactions/export");

        (await ReadCsvAsync(response)).Should().Contain("\"Almoço; sobremesa \"\"extra\"\"\"");
    }

    [Fact]
    public async Task ExportTransactions_ComDoisUsuariosDeContasDiferentes_CadaUmExportaSomenteSuasTransacoes()
    {
        AuthenticateAs("user-id-A");
        await _client.GetAsync("/transactions/export");

        AuthenticateAs("user-id-B");
        await _client.GetAsync("/transactions/export");

        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-A"), Arg.Any<CancellationToken>());
        await _factory.TransactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.AccountId == "user-id-B"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MembershipRole.Leitura)]
    [InlineData(MembershipRole.Lancar)]
    [InlineData(MembershipRole.Total)]
    [InlineData(MembershipRole.Titular)]
    public async Task ExportTransactions_ComQualquerPapel_Retorna200(MembershipRole role)
    {
        AuthenticateWithRole("user-id-123", role);

        var response = await _client.GetAsync("/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExportTransactions_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/transactions/export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.TransactionRepositoryMock.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }
}
