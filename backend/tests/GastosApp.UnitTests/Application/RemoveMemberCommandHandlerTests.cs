using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Members.Commands.RemoveMember;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RemoveMemberCommandHandlerTests
{
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly RemoveMemberCommandHandler _handler;

    public RemoveMemberCommandHandlerTests()
    {
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new RemoveMemberCommandHandler(_membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldRemoveMember_WhenExistsAndIsNotTitular()
    {
        // Arrange
        var existing = Membership.CreateInvite("account-1", "convidado@email.com", MembershipRole.Leitura);
        _membershipRepositoryMock.GetByIdAsync("account-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);
        _membershipRepositoryMock.DeleteAsync("account-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(new RemoveMemberCommand("account-1", existing.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenMemberDoesNotExist()
    {
        // Arrange
        _membershipRepositoryMock.GetByIdAsync("account-1", "id-inexistente", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _handler.Handle(new RemoveMemberCommand("account-1", "id-inexistente"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("not-found");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnCannotRemoveTitular_WhenTargetIsTitular()
    {
        // Arrange
        var titular = Membership.CreateTitular("account-1", "user-1", "titular@email.com");
        _membershipRepositoryMock.GetByIdAsync("account-1", titular.Id, Arg.Any<CancellationToken>())
            .Returns(titular);

        // Act
        var result = await _handler.Handle(new RemoveMemberCommand("account-1", titular.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cannot-remove-titular");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }
}
