using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.ComponentTests.Support;
using GastosApp.Domain.Accounts;
using NSubstitute;

namespace GastosApp.ComponentTests.Members;

public sealed class MemberEndpointsTests : IClassFixture<ComponentTestWebApplicationFactory>
{
    private readonly ComponentTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MemberEndpointsTests(ComponentTestWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMembershipRepositoryMock();
        _factory.ResetAccountRepositoryMock();
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
                "membership-caller", userId, userId, "membro@email.com", role, MembershipStatus.Ativo, DateTimeOffset.UtcNow));
    }

    // ----- GET /members -----

    [Fact]
    public async Task GetMembers_ComTitular_Retorna200ComTodosOsMembros()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        var titular = Membership.CreateTitular("titular-1", "titular-1", "titular@email.com");
        var invited = Membership.CreateInvite("titular-1", "convidado@email.com", MembershipRole.Leitura);
        _factory.MembershipRepositoryMock.ListAsync("titular-1", Arg.Any<CancellationToken>())
            .Returns(new List<Membership> { titular, invited });

        var response = await _client.GetAsync("/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    [InlineData("Total")]
    public async Task GetMembers_ComQualquerPapel_Retorna200(string role)
    {
        AuthenticateWithRole("user-1", Enum.Parse<MembershipRole>(role));
        _factory.MembershipRepositoryMock.ListAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new List<Membership>());

        var response = await _client.GetAsync("/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMembers_SemHeaderDeAutenticacao_Retorna401()
    {
        var response = await _client.GetAsync("/members");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- POST /members -----

    [Fact]
    public async Task InviteMember_ComTitularEDadosValidos_Retorna201ComLocationEBody()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        _factory.MembershipRepositoryMock.CreateInviteAsync(
                "titular-1", "convidado@email.com", MembershipRole.Leitura, Arg.Any<CancellationToken>())
            .Returns(call => MembershipWriteResult.Success(
                Membership.CreateInvite("titular-1", "convidado@email.com", MembershipRole.Leitura)));

        var response = await _client.PostAsJsonAsync("/members", new { email = "convidado@email.com", role = "Leitura" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/members/");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be("convidado@email.com");
        body.GetProperty("role").GetString().Should().Be("Leitura");
        body.GetProperty("status").GetString().Should().Be("ConvitePendente");
    }

    [Fact]
    public async Task InviteMember_ComEmailJaMembro_Retorna409()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        _factory.MembershipRepositoryMock.CreateInviteAsync(
                "titular-1", Arg.Any<string>(), Arg.Any<MembershipRole>(), Arg.Any<CancellationToken>())
            .Returns(MembershipWriteResult.EmailConflict());

        var response = await _client.PostAsJsonAsync("/members", new { email = "existente@email.com", role = "Total" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/member-already-exists");
    }

    [Theory]
    [InlineData("", "Leitura")]
    [InlineData("nao-e-email", "Leitura")]
    [InlineData("convidado@email.com", "")]
    [InlineData("convidado@email.com", "Titular")]
    [InlineData("convidado@email.com", "Admin")]
    public async Task InviteMember_ComDadosInvalidos_Retorna400SemChamarRepositorio(string email, string role)
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);

        var response = await _client.PostAsJsonAsync("/members", new { email, role });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.MembershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .CreateInviteAsync(default!, default!, default, default);
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    [InlineData("Total")]
    public async Task InviteMember_ComPapelSemPermissao_Retorna403SemChamarRepositorio(string role)
    {
        AuthenticateWithRole("user-1", Enum.Parse<MembershipRole>(role));

        var response = await _client.PostAsJsonAsync("/members", new { email = "convidado@email.com", role = "Leitura" });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/insufficient-permission");

        await _factory.MembershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .CreateInviteAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task InviteMember_SemHeaderDeAutenticacao_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/members", new { email = "convidado@email.com", role = "Leitura" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ----- PUT /members/{id} -----

    [Fact]
    public async Task UpdateMemberRole_ComTitularEDadosValidos_Retorna200()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        var existing = Membership.CreateInvite("titular-1", "convidado@email.com", MembershipRole.Leitura);
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);
        var updated = Membership.Restore(
            existing.Id, "titular-1", null, "convidado@email.com",
            MembershipRole.Total, MembershipStatus.ConvitePendente, existing.CreatedAt);
        _factory.MembershipRepositoryMock.UpdateRoleAsync("titular-1", existing.Id, MembershipRole.Total, Arg.Any<CancellationToken>())
            .Returns(MembershipWriteResult.Success(updated));

        var response = await _client.PutAsJsonAsync($"/members/{existing.Id}", new { role = "Total" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("role").GetString().Should().Be("Total");
    }

    [Fact]
    public async Task UpdateMemberRole_ComIdInexistente_Retorna404()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", "id-inexistente", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        var response = await _client.PutAsJsonAsync("/members/id-inexistente", new { role = "Total" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_NoTitular_Retorna422()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        var titular = Membership.CreateTitular("titular-1", "titular-1", "titular@email.com");
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", titular.Id, Arg.Any<CancellationToken>())
            .Returns(titular);

        var response = await _client.PutAsJsonAsync($"/members/{titular.Id}", new { role = "Total" });

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/cannot-modify-titular");
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    [InlineData("Total")]
    public async Task UpdateMemberRole_ComPapelSemPermissao_Retorna403(string role)
    {
        AuthenticateWithRole("user-1", Enum.Parse<MembershipRole>(role));

        var response = await _client.PutAsJsonAsync("/members/membership-1", new { role = "Total" });

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.MembershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default!, default);
    }

    // ----- DELETE /members/{id} -----

    [Fact]
    public async Task RemoveMember_ComTitular_Retorna204()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        var existing = Membership.CreateInvite("titular-1", "convidado@email.com", MembershipRole.Leitura);
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);
        _factory.MembershipRepositoryMock.DeleteAsync("titular-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _client.DeleteAsync($"/members/{existing.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_ComIdInexistente_Retorna404()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", "id-inexistente", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        var response = await _client.DeleteAsync("/members/id-inexistente");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMember_NoTitular_Retorna422()
    {
        AuthenticateWithRole("titular-1", MembershipRole.Titular);
        var titular = Membership.CreateTitular("titular-1", "titular-1", "titular@email.com");
        _factory.MembershipRepositoryMock.GetByIdAsync("titular-1", titular.Id, Arg.Any<CancellationToken>())
            .Returns(titular);

        var response = await _client.DeleteAsync($"/members/{titular.Id}");

        response.StatusCode.Should().Be((HttpStatusCode)422);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("type").GetString().Should().Be("https://gastosapp.dev/errors/cannot-remove-titular");
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    [InlineData("Total")]
    public async Task RemoveMember_ComPapelSemPermissao_Retorna403(string role)
    {
        AuthenticateWithRole("user-1", Enum.Parse<MembershipRole>(role));

        var response = await _client.DeleteAsync("/members/membership-1");

        response.StatusCode.Should().Be((HttpStatusCode)403);
        await _factory.MembershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task RemoveMember_SemHeaderDeAutenticacao_Retorna401()
    {
        var response = await _client.DeleteAsync("/members/membership-1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
