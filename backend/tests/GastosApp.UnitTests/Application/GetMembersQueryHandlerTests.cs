using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Members.Queries.GetMembers;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetMembersQueryHandlerTests
{
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly GetMembersQueryHandler _handler;

    public GetMembersQueryHandlerTests()
    {
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new GetMembersQueryHandler(_membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoMembersFound()
    {
        _membershipRepositoryMock.ListAsync("account-1", Arg.Any<CancellationToken>())
            .Returns(new List<Membership>());

        var result = await _handler.Handle(new GetMembersQuery("account-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnTitularAndInvitedMembers()
    {
        var titular = Membership.CreateTitular("account-1", "user-1", "titular@email.com");
        var invited = Membership.CreateInvite("account-1", "convidado@email.com", MembershipRole.Leitura);
        _membershipRepositoryMock.ListAsync("account-1", Arg.Any<CancellationToken>())
            .Returns(new List<Membership> { titular, invited });

        var result = await _handler.Handle(new GetMembersQuery("account-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(m => m.Role == "Titular" && m.Status == "Ativo");
        result.Value.Items.Should().Contain(m => m.Role == "Leitura" && m.Status == "ConvitePendente");
    }
}
