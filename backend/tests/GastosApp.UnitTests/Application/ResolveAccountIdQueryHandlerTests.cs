using FluentAssertions;
using GastosApp.Application.Accounts.Queries.ResolveAccountId;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class ResolveAccountIdQueryHandlerTests
{
    private readonly IAccountRepository _accountRepositoryMock;
    private readonly ResolveAccountIdQueryHandler _handler;

    public ResolveAccountIdQueryHandlerTests()
    {
        _accountRepositoryMock = Substitute.For<IAccountRepository>();
        _handler = new ResolveAccountIdQueryHandler(_accountRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountId_WhenResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns("account-1");

        // Act
        var result = await _handler.Handle(new ResolveAccountIdQuery("user-1"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("account-1");
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountNotFound_WhenNotResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var result = await _handler.Handle(new ResolveAccountIdQuery("user-sem-conta"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("account-not-found");
    }

    [Fact]
    public async Task Handle_ShouldNeverCreateAccount()
    {
        // Arrange — diferente de EnsureAccountCommand, esta query nunca cria.
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-sem-conta", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _handler.Handle(new ResolveAccountIdQuery("user-sem-conta"), CancellationToken.None);

        // Assert
        await _accountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }
}
