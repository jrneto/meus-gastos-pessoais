using FluentAssertions;
using GastosApp.IntegrationTests.Support;

namespace GastosApp.IntegrationTests.Members;

/// <summary>
/// Módulo Membros/convites (FEAT-20) — primeiro consumidor de
/// <see cref="TestAccountFixture.InviteAndAcceptAsync"/>, então valida
/// de ponta a ponta o mecanismo de segunda conta (fixture + limpeza)
/// antes dos módulos Transações/Categorias dependerem dele. Ver
/// backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MembersFlowTests
{
    [Fact]
    public async Task PostGetPutDelete_FluxoCompleto_FuncionaContraApiReal()
    {
        await using var titular = await TestAccountFixture.CreateAsync();

        var convidadoEmail = $"int-test+{Guid.NewGuid():N}@jrnexpenses.com";

        var postResponse = await titular.Transport.SendAsync(
            HttpMethod.Post, "/members",
            new MemberRequestDto(convidadoEmail, "Leitura"),
            bearerToken: titular.AccessToken);

        postResponse.StatusCode.Should().Be(201);
        var created = postResponse.Deserialize<MemberResponseDto>();
        created.Email.Should().Be(convidadoEmail);
        created.Role.Should().Be("Leitura");
        created.Status.Should().Be("ConvitePendente");

        var getResponse = await titular.Transport.SendAsync(
            HttpMethod.Get, "/members", bearerToken: titular.AccessToken);

        getResponse.StatusCode.Should().Be(200);
        var list = getResponse.Deserialize<MemberListResponseDto>();
        list.Items.Should().Contain(m => m.Id == created.Id && m.Status == "ConvitePendente");

        var putResponse = await titular.Transport.SendAsync(
            HttpMethod.Put, $"/members/{created.Id}",
            new MemberRoleRequestDto("Total"),
            bearerToken: titular.AccessToken);

        putResponse.StatusCode.Should().Be(200);
        var updated = putResponse.Deserialize<MemberResponseDto>();
        updated.Role.Should().Be("Total");

        var deleteResponse = await titular.Transport.SendAsync(
            HttpMethod.Delete, $"/members/{created.Id}",
            bearerToken: titular.AccessToken);

        deleteResponse.StatusCode.Should().Be(204);

        var getAfterDeleteResponse = await titular.Transport.SendAsync(
            HttpMethod.Get, "/members", bearerToken: titular.AccessToken);

        getAfterDeleteResponse.Deserialize<MemberListResponseDto>()
            .Items.Should().NotContain(m => m.Id == created.Id);
    }

    [Fact]
    public async Task InviteAndAccept_ConvitePendenteAceitoNoLogin_MembershipFicaAtiva()
    {
        await using var titular = await TestAccountFixture.CreateAsync();

        // O próprio InviteAndAcceptAsync já exercita convite (POST
        // /members) + register + AdminConfirmSignUp + login real da
        // segunda identidade — chegar aqui sem lançar já valida boa parte
        // do fluxo de aceite automático (EnsureAccountCommand +
        // AcceptPendingInvitesCommand, FEAT-20).
        await using var membro = await titular.InviteAndAcceptAsync("Leitura");

        membro.UserId.Should().NotBeNullOrWhiteSpace();
        membro.AccessToken.Should().NotBeNullOrWhiteSpace();

        var getResponse = await titular.Transport.SendAsync(
            HttpMethod.Get, "/members", bearerToken: titular.AccessToken);

        getResponse.StatusCode.Should().Be(200);
        var list = getResponse.Deserialize<MemberListResponseDto>();
        list.Items.Should().Contain(m => m.Email == membro.Email && m.Status == "Ativo" && m.Role == "Leitura");
    }

    [Fact]
    public async Task Members_ChamadoPorNaoTitular_Retorna403()
    {
        await using var titular = await TestAccountFixture.CreateAsync();
        await using var membro = await titular.InviteAndAcceptAsync("Total");

        var postResponse = await membro.Transport.SendAsync(
            HttpMethod.Post, "/members",
            new MemberRequestDto($"int-test+{Guid.NewGuid():N}@jrnexpenses.com", "Leitura"),
            bearerToken: membro.AccessToken);
        postResponse.StatusCode.Should().Be(403);

        var membersResponse = await titular.Transport.SendAsync(
            HttpMethod.Get, "/members", bearerToken: titular.AccessToken);
        var membroId = membersResponse.Deserialize<MemberListResponseDto>()
            .Items.Single(m => m.Email == membro.Email).Id;

        var putResponse = await membro.Transport.SendAsync(
            HttpMethod.Put, $"/members/{membroId}",
            new MemberRoleRequestDto("Leitura"),
            bearerToken: membro.AccessToken);
        putResponse.StatusCode.Should().Be(403);

        var deleteResponse = await membro.Transport.SendAsync(
            HttpMethod.Delete, $"/members/{membroId}",
            bearerToken: membro.AccessToken);
        deleteResponse.StatusCode.Should().Be(403);
    }
}
