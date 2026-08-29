using FluentAssertions;
using GastosApp.Application.Accounts.Commands.EnsureAccount;
using GastosApp.Application.Common.Interfaces;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class EnsureAccountCommandHandlerTests
{
    private readonly IAccountRepository _accountRepositoryMock;
    private readonly EnsureAccountCommandHandler _handler;

    public EnsureAccountCommandHandlerTests()
    {
        _accountRepositoryMock = Substitute.For<IAccountRepository>();
        _handler = new EnsureAccountCommandHandler(_accountRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingAccount_WhenAlreadyResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns("account-existente");

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-existente");
        result.Value.AlreadyExisted.Should().BeTrue();

        await _accountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldCreateAccountWithEmail_WhenNoneExistsYet()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-novo");
        result.Value.AlreadyExisted.Should().BeFalse();

        await _accountRepositoryMock.Received(1).CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnWinnerAccount_WhenCreateResolvesConcurrentConflict()
    {
        // Arrange — corrida: FindAccountIdByUserIdAsync não achou, mas
        // CreateAsync recuperou o vencedor de uma criação concorrente
        // (ex.: trigger do Cognito criou entre o Find e o Create).
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-do-vencedor", AlreadyExisted: true));

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-do-vencedor");
        result.Value.AlreadyExisted.Should().BeTrue();
    }
}
