using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Members.Queries.ResolveMembership;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class ResolveMembershipQueryHandlerTests
{
    private readonly IAccountRepository _accountRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly ResolveMembershipQueryHandler _handler;

    public ResolveMembershipQueryHandlerTests()
    {
        _accountRepositoryMock = Substitute.For<IAccountRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new ResolveMembershipQueryHandler(_accountRepositoryMock, _membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountIdMembershipIdAndRole_WhenResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns("account-1");
        var membership = Membership.Restore(
            "membership-1", "account-1", "user-1", "user1@email.com",
            MembershipRole.Total, MembershipStatus.Ativo, DateTimeOffset.UtcNow);
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        var result = await _handler.Handle(new ResolveMembershipQuery("user-1"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-1");
        result.Value.MembershipId.Should().Be("membership-1");
        result.Value.Role.Should().Be(MembershipRole.Total);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountNotFound_WhenAccountNotResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _handler.Handle(new ResolveMembershipQuery("user-sem-conta"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("account-not-found");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .FindByAccountAndUserIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountNotFound_WhenAccountResolvesButMembershipDoesNot()
    {
        // Arrange — inconsistência de dado (não deveria ocorrer em uso normal).
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns("account-1");
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-1", "user-1", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _handler.Handle(new ResolveMembershipQuery("user-1"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("account-not-found");
    }

    [Fact]
    public async Task Handle_ShouldNeverCreateAnything()
    {
        // Arrange — diferente de EnsureAccountCommand, esta query nunca cria.
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _handler.Handle(new ResolveMembershipQuery("user-sem-conta"), CancellationToken.None);

        // Assert
        await _accountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }
}
