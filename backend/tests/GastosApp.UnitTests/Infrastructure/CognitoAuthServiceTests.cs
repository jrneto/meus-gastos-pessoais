using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FluentAssertions;
using GastosApp.Application.Common.Results;
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
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("sub-123");
        result.Value.Email.Should().Be("neto@email.com");
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnConflictFailure_WhenCognitoThrowsUsernameExistsException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SignUpResponse>(new UsernameExistsException("User already exists")));

        // Act
        var result = await service.RegisterAsync("neto@email.com", "Senha123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("email-already-exists");
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
                    ExpiresIn = 3600,
                    RefreshToken = "refresh-token-123"
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
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token-123");
        result.Value.ExpiresIn.Should().Be(3600);
        result.Value.UserId.Should().Be("sub-123");
        result.Value.RefreshToken.Should().Be("refresh-token-123");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnUnauthorizedFailure_WhenCognitoThrowsNotAuthorizedException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new NotAuthorizedException("Invalid credentials")));

        // Act
        var result = await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("invalid-credentials");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnUnauthorizedFailure_WhenCognitoThrowsUserNotFoundException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new UserNotFoundException("User not found")));

        // Act
        var result = await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("invalid-credentials");
    }

    [Fact]
    public async Task RefreshAsync_ShouldRefreshSuccessfully_WhenCognitoCallSucceeds()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(new InitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType
                {
                    IdToken = "new-access-token-123",
                    ExpiresIn = 3600
                }
            });

        _cognitoMock.GetUserAsync(Arg.Any<GetUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetUserResponse
            {
                Username = "sub-123",
                UserAttributes =
                [
                    new AttributeType { Name = "sub", Value = "sub-123" }
                ]
            });

        // Act
        var result = await service.RefreshAsync("refresh-token-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token-123");
        result.Value.ExpiresIn.Should().Be(3600);
        result.Value.UserId.Should().Be("sub-123");
    }

    [Fact]
    public async Task RefreshAsync_ShouldReturnUnauthorizedFailure_WhenCognitoThrowsNotAuthorizedException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _options);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new NotAuthorizedException("Refresh token expired")));

        // Act
        var result = await service.RefreshAsync("refresh-token-expirado");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("invalid-refresh-token");
    }
}
