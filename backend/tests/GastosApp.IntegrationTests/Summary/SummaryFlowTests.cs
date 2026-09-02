using FluentAssertions;
using GastosApp.IntegrationTests.Categories;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Summary;

/// <summary>
/// Módulo Resumo mensal (FEAT-23). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SummaryFlowTests
{
    // Mês fixo e distante do calendário real — evita qualquer efeito de
    // borda com "mês corrente" e mantém o teste determinístico.
    private const string Month = "2026-08";
    private const string DateInMonth = "2026-08-15";

    private static async Task<CategoryResponseDto> CreateCategoryAsync(
        TestAccountFixture account, string nome, string tipo, long? orcamentoMensalCents)
    {
        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto(nome, tipo, orcamentoMensalCents),
            bearerToken: account.AccessToken);
        response.StatusCode.Should().Be(201);
        return response.Deserialize<CategoryResponseDto>();
    }

    [Fact]
    public async Task GetSummary_ComDadosReais_RetornaTotaisCorretos()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var categoriaDespesa = await CreateCategoryAsync(account, "Categoria Resumo Despesa", "despesa", 80000);
        var categoriaReceita = await CreateCategoryAsync(account, "Categoria Resumo Receita", "receita", null);

        await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Supermercado", 30670, categoriaDespesa.Id, "despesa", DateInMonth),
            bearerToken: account.AccessToken);

        await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Salário", 520000, categoriaReceita.Id, "receita", DateInMonth),
            bearerToken: account.AccessToken);

        var summaryResponse = await account.Transport.SendAsync(
            HttpMethod.Get, $"/summary?month={Month}", bearerToken: account.AccessToken);

        summaryResponse.StatusCode.Should().Be(200);
        var summary = summaryResponse.Deserialize<SummaryResponseDto>();

        summary.GastoCents.Should().Be(30670);
        summary.ReceitasCents.Should().Be(520000);
        summary.SaldoCents.Should().Be(520000 - 30670);
        summary.OrcamentoTotalCents.Should().Be(80000);
        summary.RestanteCents.Should().Be(80000 - 30670);
        summary.PorCategoria.Should().Contain(c => c.CategoryId == categoriaDespesa.Id && c.GastoCents == 30670);
        summary.UltimosLancamentos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSummary_MesSemDados_RetornaZerado()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var response = await account.Transport.SendAsync(
            HttpMethod.Get, "/summary?month=2026-01", bearerToken: account.AccessToken);

        response.StatusCode.Should().Be(200);
        var summary = response.Deserialize<SummaryResponseDto>();
        summary.SaldoCents.Should().Be(0);
        summary.ReceitasCents.Should().Be(0);
        summary.GastoCents.Should().Be(0);
        summary.UltimosLancamentos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSummary_ChamadoPorLeitura_Retorna200()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        await using var membro = await titular.InviteAndAcceptAsync("Leitura");

        var response = await membro.Transport.SendAsync(
            HttpMethod.Get, $"/summary?month={Month}", bearerToken: membro.AccessToken);

        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetSummary_IsolamentoEntreContas_NaoRefleteDadosDeOutraConta()
    {
        await using var contaA = await TestAccountFixture.CreateAsync();
        await using var contaB = await TestAccountFixture.CreateAsync();

        var categoriaA = await CreateCategoryAsync(contaA, "Categoria Resumo Isolamento", "despesa", null);
        await contaA.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Despesa da Conta A", 10000, categoriaA.Id, "despesa", DateInMonth),
            bearerToken: contaA.AccessToken);

        var summaryContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, $"/summary?month={Month}", bearerToken: contaB.AccessToken);

        summaryContaBResponse.StatusCode.Should().Be(200);
        summaryContaBResponse.Deserialize<SummaryResponseDto>().GastoCents.Should().Be(0);
    }
}
