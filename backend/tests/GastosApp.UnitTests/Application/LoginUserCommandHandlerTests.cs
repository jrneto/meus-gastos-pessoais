using FluentAssertions;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Application.Common.Interfaces;
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
    public async Task HandleAsync_ShouldLoginSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new LoginUserCommand("neto@email.com", "Senha123");
        var expectedResult = new LoginResult("token-jwt-123", 3600, "user-id-123");

        _authServiceMock.LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("token-jwt-123");
        result.ExpiresIn.Should().Be(3600);
        result.UserId.Should().Be("user-id-123");

        await _authServiceMock.Received(1).LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Senha123", "Email")]
    [InlineData("   ", "Senha123", "Email")]
    [InlineData("neto@email.com", "", "Senha")]
    [InlineData("neto@email.com", "   ", "Senha")]
    public async Task HandleAsync_ShouldThrowArgumentException_WhenCommandIsInvalid(string email, string password, string expectedPart)
    {
        // Arrange
        var command = new LoginUserCommand(email, password);

        // Act
        Func<Task> act = async () => await _handler.HandleAsync(command);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain(expectedPart);

        await _authServiceMock.DidNotReceiveWithAnyArgs().LoginAsync(default!, default!, default);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowInvalidCredentialsException_WhenCredentialsAreInvalid()
    {
        // Arrange
        var command = new LoginUserCommand("neto@email.com", "SenhaIncorreta");

        _authServiceMock.LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LoginResult>(new InvalidCredentialsException()));

        // Act
        Func<Task> act = async () => await _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        await _authServiceMock.Received(1).LoginAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }
}
