using FluentAssertions;
using GastosApp.Application.Auth;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterUserCommandHandlerTests
{
    private readonly IAuthService _authServiceMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _authServiceMock = Substitute.For<IAuthService>();
        _handler = new RegisterUserCommandHandler(_authServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldRegisterUserSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterUserCommand("neto@email.com", "Senha123");
        var expectedResult = new RegisterResult("user-id-123", command.Email);

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedResult));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("user-id-123");
        result.Value.Email.Should().Be(command.Email);

        await _authServiceMock.Received(1).RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Senha123",  "Email")]
    [InlineData("   ", "Senha123", "Email")]
    [InlineData("neto@email.com", "", "Senha")]
    [InlineData("neto@email.com", "   ", "Senha")]
    [InlineData("neto@email.com", "123", "Senha")]
    public async Task Handle_ShouldReturnValidationFailure_WhenCommandIsInvalid(string email, string password, string expectedPart)
    {
        // Arrange
        var command = new RegisterUserCommand(email, password);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain(expectedPart);

        await _authServiceMock.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictFailure_WhenEmailIsAlreadyRegistered()
    {
        // Arrange
        var command = new RegisterUserCommand("neto@email.com", "Senha123");

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("email-already-exists");

        await _authServiceMock.Received(1).RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
    }
}
