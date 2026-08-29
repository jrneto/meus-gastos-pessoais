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

namespace GastosApp.ComponentTests.Categories;

public sealed class CategoryEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CategoryEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetCategoryRepositoryMock();
        _factory.ResetTransactionRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _factory.ResetMembershipRepositoryMock();
        _client = factory.CreateClient();
    }

    private void AuthenticateAs(string userId, string email = "neto@email.com", string name = "Neto")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"{userId}|{email}|{name}");
    }

    private void AuthenticateWithRole(string userId, MembershipRole role)
    {
        AuthenticateAs(userId);
        _factory.MembershipRepositoryMock
            .FindByAccountAndUserIdAsync(userId, userId, Arg.Any<CancellationToken>())
            .Returns(Membership.Restore(
                "membership-1", userId, userId, "membro@email.com", role, MembershipStatus.Ativo, DateTimeOffset.UtcNow));
    }

    private static Category SampleCategory(
        string id = "category-1", string userId = "user-id-123", string nome = "Viagem",
        string tipo = "despesa", long? orcamentoMensalCents = null) =>
        Category.Restore(id, userId, nome, tipo, orcamentoMensalCents, DateTimeOffset.UtcNow);

    // ----- GET /categories -----

    [Fact]
    public async Task GetCategories_SemCategorias_Retorna200ComListaVazia()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", null, Arg.Any<CancellationToken>())
            .Returns(new List<Category>());

        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetCategories_ComCategoriasCadastradas_Retorna200ComItens()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", null, Arg.Any<CancellationToken>())
            .Returns(new List<Category> { SampleCategory(tipo: "despesa", orcamentoMensalCents: 80000) });

        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("nome").GetString().Should().Be("Viagem");
        items[0].GetProperty("tipo").GetString().Should().Be("despesa");
        items[0].GetProperty("orcamentoMensalCents").GetInt64().Should().Be(80000);
        items[0].TryGetProperty("cor", out _).Should().BeFalse();
        items[0].TryGetProperty("icone", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetCategories_ComFiltroDeTipoDespesa_RetornaSoDespesas()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { SampleCategory(nome: "Alimentacao", tipo: "despesa") });

        var response = await _client.GetAsync("/categories?tipo=despesa");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("tipo").GetString().Should().Be("despesa");
    }

    [Fact]
    public async Task GetCategories_ComFiltroDeTipoReceita_RetornaSoReceitas()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", "receita", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { SampleCategory(nome: "Salario", tipo: "receita") });

        var response = await _client.GetAsync("/categories?tipo=receita");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("tipo").GetString().Should().Be("receita");
    }

    [Fact]
    public async Task GetCategories_ComTipoInvalido_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.GetAsync("/categories?tipo=invalido");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().ListAsync(default!, default, default);
    }

    [Fact]
    public async Task GetCategories_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().ListAsync(default!, default, default);
    }

    // FEAT-19: ResolveAccountEndpointFilter roda antes de qualquer handler —
    // usuário autenticado sem Account resolvível (situação que só ocorreria
    // por dado corrompido/manual) nunca chega no CategoryRepositoryMock.
    [Fact]
    public async Task GetCategories_ComUsuarioSemContaResolvivel_Retorna401ComAccountNotFound()
    {
        AuthenticateAs("user-sem-conta");
        _factory.AccountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/account-not-found");

        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().ListAsync(default!, default, default);
    }

    // ----- GET /categories/{id} -----

    [Fact]
    public async Task GetCategoryById_ComCategoriaPropria_Retorna200ComCorpo()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory(nome: "Viagem"));

        var response = await _client.GetAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be("category-1");
        body.GetProperty("nome").GetString().Should().Be("Viagem");
    }

    [Fact]
    public async Task GetCategoryById_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetCategoryById_ComCategoriaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.GetAsync("/categories/category-inexistente");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    // ----- POST /categories -----

    [Fact]
    public async Task CreateCategory_ComDadosValidosSemOrcamento_Retorna201ComLocationEBody()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(call => CategoryWriteResult.Success(call.Arg<Category>()));

        var response = await _client.PostAsJsonAsync("/categories", new
        {
            nome = "Viagem",
            tipo = "despesa"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/categories/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nome").GetString().Should().Be("Viagem");
        body.GetProperty("tipo").GetString().Should().Be("despesa");
        body.GetProperty("orcamentoMensalCents").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task CreateCategory_ComOrcamentoInformado_Retorna201ComOrcamento()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(call => CategoryWriteResult.Success(call.Arg<Category>()));

        var response = await _client.PostAsJsonAsync("/categories", new
        {
            nome = "Salario",
            tipo = "receita",
            orcamentoMensalCents = 500000
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tipo").GetString().Should().Be("receita");
        body.GetProperty("orcamentoMensalCents").GetInt64().Should().Be(500000);
    }

    [Fact]
    public async Task CreateCategory_ComCorEIconeNoCorpo_Retorna201IgnorandoEssesCampos()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(call => CategoryWriteResult.Success(call.Arg<Category>()));

        var response = await _client.PostAsJsonAsync("/categories", new
        {
            nome = "Viagem",
            tipo = "despesa",
            cor = "#0EA5E9",
            icone = "plane"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("cor", out _).Should().BeFalse();
        body.TryGetProperty("icone", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CreateCategory_ComNomeDuplicado_Retorna422SemMensagemDeConflito()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        var response = await _client.PostAsJsonAsync("/categories", new { nome = "Lazer", tipo = "despesa" });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/name-conflict");
    }

    [Fact]
    public async Task CreateCategory_ComNomeVazio_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/categories", new { nome = "", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task CreateCategory_ComTipoInvalido_Retorna400SemChamarRepositorio(string tipo)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/categories", new { nome = "Viagem", tipo });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task CreateCategory_ComOrcamentoZeroOuNegativo_Retorna400SemChamarRepositorio(long orcamentoMensalCents)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync(
            "/categories", new { nome = "Viagem", tipo = "despesa", orcamentoMensalCents });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task CreateCategory_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PostAsJsonAsync("/categories", new { nome = "Viagem", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    // ----- PUT /categories/{id} -----

    [Fact]
    public async Task UpdateCategory_ComDadosValidos_Retorna200ComCorpoAtualizado()
    {
        AuthenticateAs("user-id-123");
        var updated = SampleCategory(nome: "Viagens");
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Viagens", "despesa", null, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nome").GetString().Should().Be("Viagens");
    }

    [Fact]
    public async Task UpdateCategory_DefinindoOrcamento_Retorna200ComOrcamentoNovo()
    {
        AuthenticateAs("user-id-123");
        var updated = SampleCategory(nome: "Viagem", tipo: "despesa", orcamentoMensalCents: 60000);
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Viagem", "despesa", 60000, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagem", tipo = "despesa", orcamentoMensalCents = 60000 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("orcamentoMensalCents").GetInt64().Should().Be(60000);
    }

    [Fact]
    public async Task UpdateCategory_RemovendoOrcamentoExistente_Retorna200ComOrcamentoNulo()
    {
        AuthenticateAs("user-id-123");
        var updated = SampleCategory(nome: "Viagem", tipo: "despesa", orcamentoMensalCents: null);
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Viagem", "despesa", null, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagem", tipo = "despesa", orcamentoMensalCents = (long?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("orcamentoMensalCents").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateCategory_ComCorEIconeNoCorpo_Retorna200IgnorandoEssesCampos()
    {
        AuthenticateAs("user-id-123");
        var updated = SampleCategory(nome: "Viagens");
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Viagens", "despesa", null, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1",
            new { nome = "Viagens", tipo = "despesa", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("cor", out _).Should().BeFalse();
        body.TryGetProperty("icone", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCategory_ComNomeDuplicado_Retorna422()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Lazer", "despesa", null, Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Lazer", tipo = "despesa" });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/name-conflict");
    }

    [Fact]
    public async Task UpdateCategory_ComCategoriaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.UpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long?>(),
                Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NotFound());

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task UpdateCategory_ComNomeVazio_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalido")]
    public async Task UpdateCategory_ComTipoInvalido_Retorna400SemChamarRepositorio(string tipo)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task UpdateCategory_ComOrcamentoZeroOuNegativo_Retorna400SemChamarRepositorio(long orcamentoMensalCents)
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo = "despesa", orcamentoMensalCents });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UpdateCategory_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo = "despesa" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default, default);
    }

    // ----- DELETE /categories/{id} -----

    [Fact]
    public async Task DeleteCategory_SemTransacoesAssociadas_Retorna204SemCorpo()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory(nome: "Viagem"));
        _factory.TransactionRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(false);
        _factory.CategoryRepositoryMock.DeleteAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCategory_ComTransacoesAssociadas_Retorna422SemExcluir()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory(nome: "Alimentacao"));
        _factory.TransactionRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/category-in-use");

        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteCategory_ComCategoriaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task DeleteCategory_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    // ----- Autorização por papel (FEAT-20) -----

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    public async Task CreateCategory_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-id-123", Enum.Parse<MembershipRole>(role));

        var response = await _client.PostAsJsonAsync("/categories", new { nome = "Viagem", tipo = "despesa" });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/insufficient-permission");

        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    public async Task UpdateCategory_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-id-123", Enum.Parse<MembershipRole>(role));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", tipo = "despesa" });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default, default);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    public async Task DeleteCategory_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-id-123", Enum.Parse<MembershipRole>(role));

        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetCategories_ComPapelLeitura_Retorna200()
    {
        AuthenticateWithRole("user-id-123", MembershipRole.Leitura);
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", null, Arg.Any<CancellationToken>())
            .Returns(new List<Category>());

        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
