using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Members.Commands.InviteMember;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class InviteMemberCommandHandlerTests
{
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly InviteMemberCommandHandler _handler;

    public InviteMemberCommandHandlerTests()
    {
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new InviteMemberCommandHandler(_membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnPendingMember_WhenInviteSucceeds()
    {
        // Arrange
        var command = new InviteMemberCommand("account-1", "convidado@email.com", "Leitura");
        var membership = Membership.CreateInvite("account-1", "convidado@email.com", MembershipRole.Leitura);
        _membershipRepositoryMock.CreateInviteAsync("account-1", "convidado@email.com", MembershipRole.Leitura, Arg.Any<CancellationToken>())
            .Returns(MembershipWriteResult.Success(membership));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("convidado@email.com");
        result.Value.Role.Should().Be("Leitura");
        result.Value.Status.Should().Be("ConvitePendente");
    }

    [Fact]
    public async Task Handle_ShouldReturnAlreadyExists_WhenEmailAlreadyMember()
    {
        // Arrange
        var command = new InviteMemberCommand("account-1", "existente@email.com", "Total");
        _membershipRepositoryMock.CreateInviteAsync("account-1", "existente@email.com", MembershipRole.Total, Arg.Any<CancellationToken>())
            .Returns(MembershipWriteResult.EmailConflict());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("member-already-exists");
    }
}
