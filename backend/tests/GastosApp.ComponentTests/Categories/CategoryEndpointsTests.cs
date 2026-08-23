using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
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
        _factory.ResetExpenseRepositoryMock();
        _factory.ResetAccountRepositoryMock();
        _client = factory.CreateClient();
    }

    private void AuthenticateAs(string userId, string email = "neto@email.com", string name = "Neto")
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, $"{userId}|{email}|{name}");
    }

    private static Category SampleCategory(
        string id = "category-1", string userId = "user-id-123", string nome = "Viagem") =>
        Category.Restore(id, userId, nome, "#0EA5E9", "plane", DateTimeOffset.UtcNow);

    // ----- GET /categories -----

    [Fact]
    public async Task GetCategories_SemCategorias_Retorna200ComListaVazia()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", Arg.Any<CancellationToken>())
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
        _factory.CategoryRepositoryMock.ListAsync("user-id-123", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { SampleCategory() });

        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("nome").GetString().Should().Be("Viagem");
    }

    [Fact]
    public async Task GetCategories_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.GetAsync("/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
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

        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().ListAsync(default!, default);
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
    public async Task CreateCategory_ComDadosValidos_Retorna201ComLocationEBody()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(call => CategoryWriteResult.Success(call.Arg<Category>()));

        var response = await _client.PostAsJsonAsync("/categories", new
        {
            nome = "Viagem",
            cor = "#0EA5E9",
            icone = "plane"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/categories/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nome").GetString().Should().Be("Viagem");
        body.GetProperty("cor").GetString().Should().Be("#0EA5E9");
        body.GetProperty("icone").GetString().Should().Be("plane");
    }

    [Fact]
    public async Task CreateCategory_ComNomeDuplicado_Retorna422SemMensagemDeConflito()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.CreateAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        var response = await _client.PostAsJsonAsync("/categories", new
        {
            nome = "Lazer",
            cor = "#0EA5E9",
            icone = "plane"
        });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/name-conflict");
    }

    [Fact]
    public async Task CreateCategory_ComNomeVazio_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync("/categories", new { nome = "", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task CreateCategory_ComCorForaDoFormatoHex_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync(
            "/categories", new { nome = "Viagem", cor = "azul", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task CreateCategory_ComIconeVazio_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PostAsJsonAsync(
            "/categories", new { nome = "Viagem", cor = "#0EA5E9", icone = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task CreateCategory_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PostAsJsonAsync(
            "/categories", new { nome = "Viagem", cor = "#0EA5E9", icone = "plane" });

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
                "user-id-123", "category-1", "Viagens", "#0EA5E9", "plane", Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("nome").GetString().Should().Be("Viagens");
    }

    [Fact]
    public async Task UpdateCategory_ComNomeDuplicado_Retorna422()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.UpdateAsync(
                "user-id-123", "category-1", "Lazer", "#0EA5E9", "plane", Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NameConflict());

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Lazer", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/name-conflict");
    }

    [Fact]
    public async Task UpdateCategory_ComCategoriaInexistenteOuDeOutroUsuario_Retorna404()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.UpdateAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(CategoryWriteResult.NotFound());

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/not-found");
    }

    [Fact]
    public async Task UpdateCategory_ComNomeVazio_Retorna400SemChamarRepositorio()
    {
        AuthenticateAs("user-id-123");

        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task UpdateCategory_SemHeaderDeAutenticacao_Retorna401SemChamarRepositorio()
    {
        var response = await _client.PutAsJsonAsync(
            "/categories/category-1", new { nome = "Viagens", cor = "#0EA5E9", icone = "plane" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.CategoryRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateAsync(default!, default!, default!, default!, default!, default);
    }

    // ----- DELETE /categories/{id} -----

    [Fact]
    public async Task DeleteCategory_SemDespesasAssociadas_Retorna204SemCorpo()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory(nome: "Viagem"));
        _factory.ExpenseRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(false);
        _factory.CategoryRepositoryMock.DeleteAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync("/categories/category-1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCategory_ComDespesasAssociadas_Retorna422SemExcluir()
    {
        AuthenticateAs("user-id-123");
        _factory.CategoryRepositoryMock.GetByIdAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
            .Returns(SampleCategory(nome: "Alimentacao"));
        _factory.ExpenseRepositoryMock.ExistsByCategoryAsync("user-id-123", "category-1", Arg.Any<CancellationToken>())
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
}
