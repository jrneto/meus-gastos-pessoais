using FluentAssertions;
using GastosApp.IntegrationTests.Categories;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Transactions;

/// <summary>
/// Módulo Exportação CSV de transações (FEAT-25). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ExportFlowTests
{
    // O corpo vem em UTF-8 com BOM, por contrato (FEAT-25) — o caractere
    // de BOM (U+FEFF) precisa ser removido do início antes de comparar
    // com o cabeçalho esperado.
    private static string[] SplitCsvLines(string body) =>
        body.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<CategoryResponseDto> CreateCategoryAsync(TestAccountFixture account, string nome)
    {
        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto(nome, "despesa", null),
            bearerToken: account.AccessToken);
        response.StatusCode.Should().Be(201);
        return response.Deserialize<CategoryResponseDto>();
    }

    [Fact]
    public async Task GetExport_ComDadosReais_RetornaCsvComLinhaCorreta()
    {
        await using var account = await TestAccountFixture.CreateAsync();
        var categoria = await CreateCategoryAsync(account, "Categoria Export Despesa");

        await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Almoço no restaurante", 4590, categoria.Id, "despesa", "2026-08-15"),
            bearerToken: account.AccessToken);

        var response = await account.Transport.SendAsync(
            HttpMethod.Get, "/transactions/export", bearerToken: account.AccessToken);

        response.StatusCode.Should().Be(200);

        // Busca o header sem depender do casing exato — DirectHttpTransport
        // (Hom/Prod) usa dicionário case-insensitive, mas LambdaRieTransport
        // (Local) reflete o casing bruto devolvido pelo Runtime Interface
        // Emulator, que pode diferir.
        var contentType = response.Headers
            .FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            .Value;
        contentType.Should().Contain("text/csv");

        var lines = SplitCsvLines(response.Body);
        lines[0].Should().Be("data;descricao;categoria;tipo;valor;lancadoPor");
        lines.Should().ContainSingle(l =>
            l.Contains("2026-08-15") &&
            l.Contains("Almoço no restaurante") &&
            l.Contains(categoria.Nome) &&
            l.Contains("despesa") &&
            l.Contains("45,90") &&
            l.Contains("Você"));
    }

    [Fact]
    public async Task GetExport_SemResultado_RetornaCsvSoComCabecalho()
    {
        await using var account = await TestAccountFixture.CreateAsync();
        var categoria = await CreateCategoryAsync(account, "Categoria Export Sem Resultado");

        await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Almoço", 4590, categoria.Id, "despesa", "2026-08-15"),
            bearerToken: account.AccessToken);

        // Conta só tem despesas — filtrar por tipo=receita não deve
        // trazer nenhuma linha além do cabeçalho.
        var response = await account.Transport.SendAsync(
            HttpMethod.Get, "/transactions/export?tipo=receita", bearerToken: account.AccessToken);

        response.StatusCode.Should().Be(200);
        var lines = SplitCsvLines(response.Body);
        lines.Should().ContainSingle();
        lines[0].Should().Be("data;descricao;categoria;tipo;valor;lancadoPor");
    }

    [Fact]
    public async Task GetExport_ChamadoPorLeitura_Retorna200()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        await using var membro = await titular.InviteAndAcceptAsync("Leitura");

        var response = await membro.Transport.SendAsync(
            HttpMethod.Get, "/transactions/export", bearerToken: membro.AccessToken);

        response.StatusCode.Should().Be(200);
    }
}
