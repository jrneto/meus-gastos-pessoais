using FluentAssertions;
using GastosApp.IntegrationTests.Categories;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Reports;

/// <summary>
/// Módulo Relatórios por período (FEAT-24). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ReportsFlowTests
{
    private const string DateInMonth = "2026-08-15";

    private static async Task<CategoryResponseDto> CreateCategoryAsync(
        TestAccountFixture account, string nome, long? orcamentoMensalCents)
    {
        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto(nome, "despesa", orcamentoMensalCents),
            bearerToken: account.AccessToken);
        response.StatusCode.Should().Be(201);
        return response.Deserialize<CategoryResponseDto>();
    }

    [Fact]
    public async Task GetReports_PeriodoMensalComDespesaReal_RetornaTotaisCorretos()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var categoria = await CreateCategoryAsync(account, "Categoria Relatorio Despesa", 80000);

        await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Supermercado", 43510, categoria.Id, "despesa", DateInMonth),
            bearerToken: account.AccessToken);

        var response = await account.Transport.SendAsync(
            HttpMethod.Get, $"/reports?period=month&date={DateInMonth}", bearerToken: account.AccessToken);

        response.StatusCode.Should().Be(200);
        var reports = response.Deserialize<ReportsResponseDto>();

        reports.TotalCents.Should().Be(43510);
        reports.PorCategoria.Should().ContainSingle(c => c.CategoryId == categoria.Id && c.GastoCents == 43510);
        reports.MaiorGasto.Should().NotBeNull();
        reports.MaiorGasto!.CategoryId.Should().Be(categoria.Id);
        reports.MaiorGasto.GastoCents.Should().Be(43510);
        reports.MaiorGasto.PercentualOrcamento.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReports_ChamadoPorLeitura_Retorna200()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        await using var membro = await titular.InviteAndAcceptAsync("Leitura");

        var response = await membro.Transport.SendAsync(
            HttpMethod.Get, $"/reports?period=month&date={DateInMonth}", bearerToken: membro.AccessToken);

        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetReports_IsolamentoEntreContas_NaoRefleteDadosDeOutraConta()
    {
        await using var contaA = await TestAccountFixture.CreateAsync();
        await using var contaB = await TestAccountFixture.CreateAsync();

        var categoriaA = await CreateCategoryAsync(contaA, "Categoria Relatorio Isolamento", null);
        await contaA.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Despesa da Conta A", 10000, categoriaA.Id, "despesa", DateInMonth),
            bearerToken: contaA.AccessToken);

        var reportsContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, $"/reports?period=month&date={DateInMonth}", bearerToken: contaB.AccessToken);

        reportsContaBResponse.StatusCode.Should().Be(200);
        var reports = reportsContaBResponse.Deserialize<ReportsResponseDto>();
        reports.TotalCents.Should().Be(0);
        reports.PorCategoria.Should().BeEmpty();
    }
}
