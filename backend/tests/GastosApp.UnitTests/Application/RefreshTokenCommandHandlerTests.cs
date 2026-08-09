using FluentAssertions;
using GastosApp.Application.Auth;
using GastosApp.Application.Auth.Commands.Refresh;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RefreshTokenCommandHandlerTests
{
    private readonly IAuthService _authServiceMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _authServiceMock = Substitute.For<IAuthService>();
        _handler = new RefreshTokenCommandHandler(_authServiceMock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldReturnRefreshTokenMissingFailure_WhenTokenIsEmpty(string refreshToken)
    {
        // Arrange
        var command = new RefreshTokenCommand(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("refresh-token-missing");

        await _authServiceMock.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldRefreshSuccessfully_WhenTokenIsValid()
    {
        // Arrange
        var command = new RefreshTokenCommand("refresh-token-123");
        var expectedResult = new RefreshResult("new-access-token", 3600, "user-id-123");

        _authServiceMock.RefreshAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedResult));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token");
        result.Value.ExpiresIn.Should().Be(3600);
        result.Value.UserId.Should().Be("user-id-123");

        await _authServiceMock.Received(1).RefreshAsync(command.RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPropagateFailure_WhenAuthServiceReturnsInvalidRefreshToken()
    {
        // Arrange
        var command = new RefreshTokenCommand("refresh-token-invalido");

        _authServiceMock.RefreshAsync(command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("invalid-refresh-token");
    }
}
