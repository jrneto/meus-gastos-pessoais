using FluentAssertions;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Application.Common.Interfaces;
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
    public async Task HandleAsync_ShouldRegisterUserSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterUserCommand("neto@email.com", "Senha123", "Neto");
        var expectedResult = new RegisterResult("user-id-123", command.Email, command.Name);

        _authServiceMock.RegisterAsync(command.Email, command.Password, command.Name, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("user-id-123");
        result.Email.Should().Be(command.Email);
        result.Name.Should().Be(command.Name);

        await _authServiceMock.Received(1).RegisterAsync(command.Email, command.Password, command.Name, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Senha123", "Neto", "Email")]
    [InlineData("   ", "Senha123", "Neto", "Email")]
    [InlineData("neto@email.com", "", "Neto", "Senha")]
    [InlineData("neto@email.com", "   ", "Neto", "Senha")]
    [InlineData("neto@email.com", "123", "Neto", "Senha")]
    [InlineData("neto@email.com", "Senha123", "", "Nome")]
    [InlineData("neto@email.com", "Senha123", "   ", "Nome")]
    public async Task HandleAsync_ShouldThrowArgumentException_WhenCommandIsInvalid(string email, string password, string name, string expectedPart)
    {
        // Arrange
        var command = new RegisterUserCommand(email, password, name);

        // Act
        Func<Task> act = async () => await _handler.HandleAsync(command);

        // Assert
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain(expectedPart);

        await _authServiceMock.DidNotReceiveWithAnyArgs().RegisterAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowEmailAlreadyExistsException_WhenEmailIsAlreadyRegistered()
    {
        // Arrange
        var command = new RegisterUserCommand("neto@email.com", "Senha123", "Neto");

        _authServiceMock.RegisterAsync(command.Email, command.Password, command.Name, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<RegisterResult>(new EmailAlreadyExistsException()));

        // Act
        Func<Task> act = async () => await _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<EmailAlreadyExistsException>();

        await _authServiceMock.Received(1).RegisterAsync(command.Email, command.Password, command.Name, Arg.Any<CancellationToken>());
    }
}
