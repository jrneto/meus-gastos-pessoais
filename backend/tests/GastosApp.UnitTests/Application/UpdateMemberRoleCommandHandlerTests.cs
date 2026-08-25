using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Members.Commands.UpdateMemberRole;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateMemberRoleCommandHandlerTests
{
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly UpdateMemberRoleCommandHandler _handler;

    public UpdateMemberRoleCommandHandlerTests()
    {
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new UpdateMemberRoleCommandHandler(_membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldUpdateRole_WhenMemberExistsAndIsNotTitular()
    {
        // Arrange
        var existing = Membership.CreateInvite("account-1", "convidado@email.com", MembershipRole.Leitura);
        _membershipRepositoryMock.GetByIdAsync("account-1", existing.Id, Arg.Any<CancellationToken>())
            .Returns(existing);
        var updated = Membership.Restore(
            existing.Id, "account-1", null, "convidado@email.com",
            MembershipRole.Total, MembershipStatus.ConvitePendente, existing.CreatedAt);
        _membershipRepositoryMock.UpdateRoleAsync("account-1", existing.Id, MembershipRole.Total, Arg.Any<CancellationToken>())
            .Returns(MembershipWriteResult.Success(updated));

        // Act
        var result = await _handler.Handle(new UpdateMemberRoleCommand("account-1", existing.Id, "Total"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("Total");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenMemberDoesNotExist()
    {
        // Arrange
        _membershipRepositoryMock.GetByIdAsync("account-1", "id-inexistente", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _handler.Handle(new UpdateMemberRoleCommand("account-1", "id-inexistente", "Total"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("not-found");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateRoleAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnCannotModifyTitular_WhenTargetIsTitular()
    {
        // Arrange
        var titular = Membership.CreateTitular("account-1", "user-1", "titular@email.com");
        _membershipRepositoryMock.GetByIdAsync("account-1", titular.Id, Arg.Any<CancellationToken>())
            .Returns(titular);

        // Act
        var result = await _handler.Handle(new UpdateMemberRoleCommand("account-1", titular.Id, "Total"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cannot-modify-titular");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs()
            .UpdateRoleAsync(default!, default!, default, default);
    }
}
