using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Members.Commands.AcceptPendingInvites;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class AcceptPendingInvitesCommandHandlerTests
{
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly IAccountRepository _accountRepositoryMock;
    private readonly AcceptPendingInvitesCommandHandler _handler;

    public AcceptPendingInvitesCommandHandlerTests()
    {
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _accountRepositoryMock = Substitute.For<IAccountRepository>();
        _handler = new AcceptPendingInvitesCommandHandler(_membershipRepositoryMock, _accountRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldBeNoOp_WhenNoPendingInvites()
    {
        // Arrange
        _membershipRepositoryMock.AcceptPendingInvitesByEmailAsync("user@email.com", "user-1", Arg.Any<CancellationToken>())
            .Returns(new List<AcceptedInvite>());

        // Act
        var result = await _handler.Handle(new AcceptPendingInvitesCommand("user-1", "user@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SwitchedToAccountId.Should().BeNull();

        await _accountRepositoryMock.DidNotReceiveWithAnyArgs().SetActiveAccountAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldSwitchActiveAccount_WhenOnePendingInviteAccepted()
    {
        // Arrange
        var acceptedAt = DateTimeOffset.UtcNow;
        _membershipRepositoryMock.AcceptPendingInvitesByEmailAsync("user@email.com", "user-1", Arg.Any<CancellationToken>())
            .Returns(new List<AcceptedInvite> { new("account-convite", acceptedAt) });

        // Act
        var result = await _handler.Handle(new AcceptPendingInvitesCommand("user-1", "user@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SwitchedToAccountId.Should().Be("account-convite");

        await _accountRepositoryMock.Received(1).SetActiveAccountAsync("user-1", "account-convite", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPickMostRecentInvite_WhenMultipleAccountsAccepted()
    {
        // Arrange
        var older = new AcceptedInvite("account-antigo", DateTimeOffset.UtcNow.AddDays(-2));
        var newer = new AcceptedInvite("account-novo", DateTimeOffset.UtcNow);
        _membershipRepositoryMock.AcceptPendingInvitesByEmailAsync("user@email.com", "user-1", Arg.Any<CancellationToken>())
            .Returns(new List<AcceptedInvite> { older, newer });

        // Act
        var result = await _handler.Handle(new AcceptPendingInvitesCommand("user-1", "user@email.com"), CancellationToken.None);

        // Assert
        result.Value.SwitchedToAccountId.Should().Be("account-novo");
        await _accountRepositoryMock.Received(1).SetActiveAccountAsync("user-1", "account-novo", Arg.Any<CancellationToken>());
    }
}
