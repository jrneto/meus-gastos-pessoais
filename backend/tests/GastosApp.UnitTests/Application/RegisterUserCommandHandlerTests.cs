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
    private const string Email = "neto@email.com";
    private const string Password = "Senha123";
    private const string Name = "Fulano da Silva";
    private const string PhoneNumber = "11999998888";
    private const string CpfValue = "11144477735";

    private readonly IAuthService _authServiceMock;
    private readonly IUserProfileRepository _userProfileRepositoryMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _authServiceMock = Substitute.For<IAuthService>();
        _userProfileRepositoryMock = Substitute.For<IUserProfileRepository>();
        _handler = new RegisterUserCommandHandler(_authServiceMock, _userProfileRepositoryMock);
    }

    private static RegisterUserCommand ValidCommand() => new(Email, Password, Name, PhoneNumber, CpfValue);

    [Fact]
    public async Task Handle_ShouldRegisterUserSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = ValidCommand();
        var authResult = new RegisterResult("user-id-123", command.Email);

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(authResult));
        _userProfileRepositoryMock.CreateAsync(Arg.Any<GastosApp.Domain.Users.UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserProfileResult(CpfAlreadyExists: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("user-id-123");
        result.Value.Email.Should().Be(command.Email);
        result.Value.Name.Should().Be(Name);
        result.Value.PhoneNumber.Should().Be(PhoneNumber);
        result.Value.Cpf.Should().Be(CpfValue);

        await _authServiceMock.Received(1).RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>());
        await _authServiceMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldTrimName_BeforeStoringProfile()
    {
        // Arrange
        var command = new RegisterUserCommand(Email, Password, "  Fulano da Silva  ", PhoneNumber, CpfValue);

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RegisterResult("user-id-123", command.Email)));
        _userProfileRepositoryMock.CreateAsync(Arg.Any<GastosApp.Domain.Users.UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserProfileResult(CpfAlreadyExists: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.Name.Should().Be("Fulano da Silva");
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictFailure_WhenEmailIsAlreadyRegistered()
    {
        // Arrange
        var command = ValidCommand();

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("email-already-exists");

        await _userProfileRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        await _authServiceMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflictFailureAndRollbackCognito_WhenCpfIsAlreadyRegistered()
    {
        // Arrange
        var command = ValidCommand();

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RegisterResult("user-id-123", command.Email)));
        _userProfileRepositoryMock.CreateAsync(Arg.Any<GastosApp.Domain.Users.UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserProfileResult(CpfAlreadyExists: true));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("cpf-already-exists");

        await _authServiceMock.Received(1).DeleteAsync(command.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRollbackCognitoAndRethrow_WhenProfileCreationThrowsUnexpectedException()
    {
        // Arrange
        var command = ValidCommand();
        var exception = new InvalidOperationException("erro transiente");

        _authServiceMock.RegisterAsync(command.Email, command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RegisterResult("user-id-123", command.Email)));
        _userProfileRepositoryMock.CreateAsync(Arg.Any<GastosApp.Domain.Users.UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CreateUserProfileResult>(exception));

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None).AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _authServiceMock.Received(1).DeleteAsync(command.Email, Arg.Any<CancellationToken>());
    }
}
