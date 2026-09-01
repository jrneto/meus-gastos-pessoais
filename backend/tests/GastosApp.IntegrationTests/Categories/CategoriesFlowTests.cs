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
    public async Task GetCategories_ComFiltroTipo_RetornaSomenteDoTipoFiltrado()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var despesaResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria Filtro Despesa", "despesa", null),
            bearerToken: account.AccessToken);
        var categoriaDespesa = despesaResponse.Deserialize<CategoryResponseDto>();

        var receitaResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria Filtro Receita", "receita", null),
            bearerToken: account.AccessToken);
        var categoriaReceita = receitaResponse.Deserialize<CategoryResponseDto>();

        var listDespesaResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/categories?tipo=despesa", bearerToken: account.AccessToken);
        listDespesaResponse.StatusCode.Should().Be(200);
        var listDespesa = listDespesaResponse.Deserialize<CategoryListResponseDto>();
        listDespesa.Items.Should().Contain(c => c.Id == categoriaDespesa.Id);
        listDespesa.Items.Should().NotContain(c => c.Id == categoriaReceita.Id);
        listDespesa.Items.Should().OnlyContain(c => c.Tipo == "despesa");

        var listReceitaResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/categories?tipo=receita", bearerToken: account.AccessToken);
        listReceitaResponse.StatusCode.Should().Be(200);
        var listReceita = listReceitaResponse.Deserialize<CategoryListResponseDto>();
        listReceita.Items.Should().Contain(c => c.Id == categoriaReceita.Id);
        listReceita.Items.Should().NotContain(c => c.Id == categoriaDespesa.Id);
        listReceita.Items.Should().OnlyContain(c => c.Tipo == "receita");

        // Sem filtro, continua retornando os dois tipos (comportamento já
        // coberto por PostGetPutDelete_FluxoCompleto_FuncionaContraApiReal,
        // reafirmado aqui pelo contraste direto com os filtros acima).
        var listSemFiltroResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/categories", bearerToken: account.AccessToken);
        var listSemFiltro = listSemFiltroResponse.Deserialize<CategoryListResponseDto>();
        listSemFiltro.Items.Should().Contain(c => c.Id == categoriaDespesa.Id);
        listSemFiltro.Items.Should().Contain(c => c.Id == categoriaReceita.Id);
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

    [Fact]
    public async Task DeleteCategories_ComTransacaoAssociada_Retorna422()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        // Nome deliberadamente distinto do catálogo de 13 categorias
        // padrão semeadas em toda conta nova (FEAT-28, DefaultCategorySeed)
        // — usar um nome coincidente (ex.: "Alimentação"/"Transporte")
        // colide (422 name-conflict) com a categoria já seedada.
        var categoriaResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto("Categoria de Teste com Transação", "despesa", null),
            bearerToken: account.AccessToken);
        categoriaResponse.StatusCode.Should().Be(201);
        var categoria = categoriaResponse.Deserialize<CategoryResponseDto>();

        var transacaoResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Almoço", 4590, categoria.Id, "despesa", "2026-08-15"),
            bearerToken: account.AccessToken);
        transacaoResponse.StatusCode.Should().Be(201);

        var deleteResponse = await account.Transport.SendAsync(
            HttpMethod.Delete, $"/categories/{categoria.Id}", bearerToken: account.AccessToken);
        deleteResponse.StatusCode.Should().Be(422);

        var getAfterFailedDeleteResponse = await account.Transport.SendAsync(
            HttpMethod.Get, $"/categories/{categoria.Id}", bearerToken: account.AccessToken);
        getAfterFailedDeleteResponse.StatusCode.Should().Be(200);
    }
}
