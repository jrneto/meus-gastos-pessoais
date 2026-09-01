using FluentAssertions;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Categories;

/// <summary>
/// Módulo Categorias (FEAT-16 + FEAT-21). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CategoriesFlowTests
{
    [Fact]
    public async Task PostGetPutDelete_FluxoCompleto_FuncionaContraApiReal()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var comOrcamentoResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Viagem", "despesa", 50000),
            bearerToken: account.AccessToken);

        comOrcamentoResponse.StatusCode.Should().Be(201);
        var comOrcamento = comOrcamentoResponse.Deserialize<CategoryResponseDto>();
        comOrcamento.OrcamentoMensalCents.Should().Be(50000);

        var semOrcamentoResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Salario", "receita", null),
            bearerToken: account.AccessToken);

        semOrcamentoResponse.StatusCode.Should().Be(201);
        var semOrcamento = semOrcamentoResponse.Deserialize<CategoryResponseDto>();
        semOrcamento.OrcamentoMensalCents.Should().BeNull();

        var listResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/categories", bearerToken: account.AccessToken);

        listResponse.StatusCode.Should().Be(200);
        var list = listResponse.Deserialize<CategoryListResponseDto>();
        list.Items.Should().Contain(c => c.Id == comOrcamento.Id);
        list.Items.Should().Contain(c => c.Id == semOrcamento.Id);

        var putResponse = await account.Transport.SendAsync(
            HttpMethod.Put, $"/categories/{comOrcamento.Id}",
            new CategoryRequestDto("Viagens", "despesa", 60000),
            bearerToken: account.AccessToken);

        putResponse.StatusCode.Should().Be(200);
        var updated = putResponse.Deserialize<CategoryResponseDto>();
        updated.Nome.Should().Be("Viagens");
        updated.OrcamentoMensalCents.Should().Be(60000);

        var deleteResponse = await account.Transport.SendAsync(
            HttpMethod.Delete, $"/categories/{comOrcamento.Id}",
            bearerToken: account.AccessToken);

        deleteResponse.StatusCode.Should().Be(204);

        var getAfterDeleteResponse = await account.Transport.SendAsync(
            HttpMethod.Get, $"/categories/{comOrcamento.Id}", bearerToken: account.AccessToken);
        getAfterDeleteResponse.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Categories_ChamadoPorLeitura_Retorna403EmEscrita()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        await using var membro = await titular.InviteAndAcceptAsync("Leitura");

        var postResponse = await membro.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria do Convidado", "despesa", null),
            bearerToken: membro.AccessToken);
        postResponse.StatusCode.Should().Be(403);

        var categoriaResponse = await titular.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria do Titular", "despesa", null),
            bearerToken: titular.AccessToken);
        var categoriaId = categoriaResponse.Deserialize<CategoryResponseDto>().Id;

        var putResponse = await membro.Transport.SendAsync(
            HttpMethod.Put, $"/categories/{categoriaId}",
            new CategoryRequestDto("Categoria do Titular Editada", "despesa", null),
            bearerToken: membro.AccessToken);
        putResponse.StatusCode.Should().Be(403);

        var deleteResponse = await membro.Transport.SendAsync(
            HttpMethod.Delete, $"/categories/{categoriaId}",
            bearerToken: membro.AccessToken);
        deleteResponse.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Categories_IsolamentoEntreContas_CategoriaDeUmaContaNaoApareceNaOutra()
    {
        await using var contaA = await TestAccountFixture.CreateAsync();
        await using var contaB = await TestAccountFixture.CreateAsync();

        var createResponse = await contaA.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria da Conta A", "despesa", null),
            bearerToken: contaA.AccessToken);
        var categoriaId = createResponse.Deserialize<CategoryResponseDto>().Id;

        var listContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, "/categories", bearerToken: contaB.AccessToken);
        listContaBResponse.Deserialize<CategoryListResponseDto>()
            .Items.Should().NotContain(c => c.Id == categoriaId);

        var getContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, $"/categories/{categoriaId}", bearerToken: contaB.AccessToken);
        getContaBResponse.StatusCode.Should().Be(404);
    }
}
