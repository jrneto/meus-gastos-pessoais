using FluentAssertions;
using GastosApp.IntegrationTests.Categories;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Transactions;

/// <summary>
/// Módulo Transações (FEAT-22). Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TransactionsFlowTests
{
    // Nomes de categoria usados nestes testes são deliberadamente
    // distintos do catálogo de 13 categorias padrão semeadas em toda
    // conta nova (FEAT-28, DefaultCategorySeed — inclui, entre outras,
    // "Transporte" e "Alimentação") — usar um nome coincidente colide
    // (422 name-conflict, FEAT-16) com a categoria já seedada.
    private static async Task<CategoryResponseDto> CreateCategoryAsync(
        TestAccountFixture account, string nome, string tipo)
    {
        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/categories",
            new CategoryRequestDto(nome, tipo, null),
            bearerToken: account.AccessToken);
        response.StatusCode.Should().Be(201);
        return response.Deserialize<CategoryResponseDto>();
    }

    [Fact]
    public async Task PostGetPutDelete_FluxoCompleto_FuncionaContraApiReal()
    {
        await using var account = await TestAccountFixture.CreateAsync();

        var categoriaDespesa = await CreateCategoryAsync(account, "Categoria Despesa Teste", "despesa");
        var categoriaReceita = await CreateCategoryAsync(account, "Salario", "receita");

        var despesaResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Almoço", 4590, categoriaDespesa.Id, "despesa", "2026-08-15"),
            bearerToken: account.AccessToken);

        despesaResponse.StatusCode.Should().Be(201);
        var despesa = despesaResponse.Deserialize<TransactionResponseDto>();
        despesa.Tipo.Should().Be("despesa");
        despesa.CreatedByLabel.Should().Be("Você");

        var receitaResponse = await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Salário de agosto", 500000, categoriaReceita.Id, "receita", "2026-08-05"),
            bearerToken: account.AccessToken);

        receitaResponse.StatusCode.Should().Be(201);
        var receita = receitaResponse.Deserialize<TransactionResponseDto>();
        receita.Tipo.Should().Be("receita");

        var listResponse = await account.Transport.SendAsync(
            HttpMethod.Get, "/transactions", bearerToken: account.AccessToken);
        listResponse.StatusCode.Should().Be(200);
        var list = listResponse.Deserialize<TransactionListResponseDto>();
        list.Items.Should().Contain(t => t.Id == despesa.Id);
        list.Items.Should().Contain(t => t.Id == receita.Id);

        var getResponse = await account.Transport.SendAsync(
            HttpMethod.Get, $"/transactions/{despesa.Id}", bearerToken: account.AccessToken);
        getResponse.StatusCode.Should().Be(200);

        var putResponse = await account.Transport.SendAsync(
            HttpMethod.Put, $"/transactions/{despesa.Id}",
            new TransactionRequestDto("Almoço no restaurante", 5290, categoriaDespesa.Id, "despesa", "2026-08-16"),
            bearerToken: account.AccessToken);
        putResponse.StatusCode.Should().Be(200);
        putResponse.Deserialize<TransactionResponseDto>().AmountInCents.Should().Be(5290);

        var deleteResponse = await account.Transport.SendAsync(
            HttpMethod.Delete, $"/transactions/{despesa.Id}", bearerToken: account.AccessToken);
        deleteResponse.StatusCode.Should().Be(204);

        var getAfterDeleteResponse = await account.Transport.SendAsync(
            HttpMethod.Get, $"/transactions/{despesa.Id}", bearerToken: account.AccessToken);
        getAfterDeleteResponse.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Transactions_TipoDivergenteDaCategoria_Retorna400()
    {
        await using var account = await TestAccountFixture.CreateAsync();
        var categoriaDespesa = await CreateCategoryAsync(account, "Categoria Despesa Divergente", "despesa");

        var response = await account.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Divergente", 1000, categoriaDespesa.Id, "receita", "2026-08-15"),
            bearerToken: account.AccessToken);

        response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Transactions_PapelLancar_EditaEExcluiApenasAPropria()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        var categoria = await CreateCategoryAsync(titular, "Categoria do Papel Lancar", "despesa");

        await using var membro = await titular.InviteAndAcceptAsync("Lancar");

        // Transação criada pelo Titular — o convidado (Lancar) não pode editar/excluir.
        var transacaoDoTitularResponse = await titular.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Cinema", 4000, categoria.Id, "despesa", "2026-08-10"),
            bearerToken: titular.AccessToken);
        var transacaoDoTitular = transacaoDoTitularResponse.Deserialize<TransactionResponseDto>();

        var putNaDoTitularResponse = await membro.Transport.SendAsync(
            HttpMethod.Put, $"/transactions/{transacaoDoTitular.Id}",
            new TransactionRequestDto("Cinema editado", 4500, categoria.Id, "despesa", "2026-08-10"),
            bearerToken: membro.AccessToken);
        putNaDoTitularResponse.StatusCode.Should().Be(403);

        var deleteNaDoTitularResponse = await membro.Transport.SendAsync(
            HttpMethod.Delete, $"/transactions/{transacaoDoTitular.Id}",
            bearerToken: membro.AccessToken);
        deleteNaDoTitularResponse.StatusCode.Should().Be(403);

        // Transação criada pelo próprio convidado (Lancar) — pode editar/excluir.
        var transacaoDoMembroResponse = await membro.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Show", 8000, categoria.Id, "despesa", "2026-08-11"),
            bearerToken: membro.AccessToken);
        var transacaoDoMembro = transacaoDoMembroResponse.Deserialize<TransactionResponseDto>();

        var putNaPropriaResponse = await membro.Transport.SendAsync(
            HttpMethod.Put, $"/transactions/{transacaoDoMembro.Id}",
            new TransactionRequestDto("Show editado", 8500, categoria.Id, "despesa", "2026-08-11"),
            bearerToken: membro.AccessToken);
        putNaPropriaResponse.StatusCode.Should().Be(200);

        var deleteNaPropriaResponse = await membro.Transport.SendAsync(
            HttpMethod.Delete, $"/transactions/{transacaoDoMembro.Id}",
            bearerToken: membro.AccessToken);
        deleteNaPropriaResponse.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task Transactions_IsolamentoEntreContas_TransacaoDeUmaContaNaoApareceNaOutra()
    {
        await using var contaA = await TestAccountFixture.CreateAsync();
        await using var contaB = await TestAccountFixture.CreateAsync();

        var categoriaA = await CreateCategoryAsync(contaA, "Categoria Despesa Teste", "despesa");

        var createResponse = await contaA.Transport.SendAsync(
            HttpMethod.Post, "/transactions",
            new TransactionRequestDto("Almoço", 4590, categoriaA.Id, "despesa", "2026-08-15"),
            bearerToken: contaA.AccessToken);
        var transacaoId = createResponse.Deserialize<TransactionResponseDto>().Id;

        var listContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, "/transactions", bearerToken: contaB.AccessToken);
        listContaBResponse.Deserialize<TransactionListResponseDto>()
            .Items.Should().NotContain(t => t.Id == transacaoId);

        var getContaBResponse = await contaB.Transport.SendAsync(
            HttpMethod.Get, $"/transactions/{transacaoId}", bearerToken: contaB.AccessToken);
        getContaBResponse.StatusCode.Should().Be(404);
    }
}
