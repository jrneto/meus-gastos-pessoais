using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FluentAssertions;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Infrastructure.Auth;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GastosApp.UnitTests.Infrastructure;

public class CognitoAuthServiceTests
{
    private readonly IAmazonCognitoIdentityProvider _cognitoMock;
    private readonly IOptions<CognitoOptions> _options;

    public CognitoAuthServiceTests()
    {
        _cognitoMock = Substitute.For<IAmazonCognitoIdentityProvider>();

        _options = Microsoft.Extensions.Options.Options.Create(new CognitoOptions
        {
            Region = "us-east-1",
            UserPoolId = "us-east-1_testpool",
            ClientId = "testclient"
        });
    }

    [Fact]
    public async Task RegisterAsync_ShouldRegisterSuccessfully_WhenCognitoCallSucceeds()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SignUpResponse { UserSub = "sub-123" });

        // Act
        var result = await service.RegisterAsync("neto@email.com", "Senha123");

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("sub-123");
        result.Email.Should().Be("neto@email.com");
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowEmailAlreadyExistsException_WhenCognitoThrowsUsernameExistsException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SignUpResponse>(new UsernameExistsException("User already exists")));

        // Act
        Func<Task> act = async () => await service.RegisterAsync("neto@email.com", "Senha123");

        // Assert
        await act.Should().ThrowAsync<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldLoginSuccessfully_WhenCognitoCallSucceeds()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    IdToken = "access-token-123",
                    ExpiresIn = 3600
                }
            });

        _cognitoMock.GetUserAsync(Arg.Any<GetUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetUserResponse
            {
                Username = "sub-123",
                UserAttributes =
                [
                    new AttributeType { Name = "sub",  Value = "sub-123" },
                    new AttributeType { Name = "name", Value = "Neto"    }
                ]
            });

        // Act
        var result = await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        result.Should().NotBeNull();
        result.accessToken.Should().Be("access-token-123");
        result.ExpiresIn.Should().Be(3600);
        result.UserId.Should().Be("sub-123");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WhenCognitoThrowsNotAuthorizedException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new NotAuthorizedException("Invalid credentials")));

        // Act
        Func<Task> act = async () => await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WhenCognitoThrowsUserNotFoundException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new UserNotFoundException("User not found")));

        // Act
        Func<Task> act = async () => await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }
}