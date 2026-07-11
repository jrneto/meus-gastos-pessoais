using FluentAssertions;
using GastosApp.Application.Auth;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class LoginUserCommandHandlerTests
{
    private readonly IAuthService _authServiceMock;
    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        _authServiceMock = Substitute.For<IAuthService>();
        _handler = new LoginUserCommandHandler(_authServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldLoginSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new LoginUserCommand("neto@email.com", "Senha123");
        var expectedResult = new LoginResult("token-jwt-123", 3600, "user-id-123");

        _authServiceMock.LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedResult));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("token-jwt-123");
        result.Value.ExpiresIn.Should().Be(3600);
        result.Value.UserId.Should().Be("user-id-123");

        await _authServiceMock.Received(1).LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Senha123", "Email")]
    [InlineData("   ", "Senha123", "Email")]
    [InlineData("neto@email.com", "", "Senha")]
    [InlineData("neto@email.com", "   ", "Senha")]
    public async Task Handle_ShouldReturnValidationFailure_WhenCommandIsInvalid(string email, string password, string expectedPart)
    {
        // Arrange
        var command = new LoginUserCommand(email, password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain(expectedPart);

        await _authServiceMock.DidNotReceiveWithAnyArgs().LoginAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorizedFailure_WhenCredentialsAreInvalid()
    {
        // Arrange
        var command = new LoginUserCommand("neto@email.com", "SenhaIncorreta");

        _authServiceMock.LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LoginResult>(AuthErrors.InvalidCredentials));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("invalid-credentials");

        await _authServiceMock.Received(1).LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }
}
