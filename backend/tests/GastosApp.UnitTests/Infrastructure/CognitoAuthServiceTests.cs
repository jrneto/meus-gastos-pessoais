using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using FluentAssertions;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Infrastructure;

public class CognitoAuthServiceTests
{
    private readonly IAmazonCognitoIdentityProvider _cognitoMock;
    private readonly IConfiguration _configMock;

    public CognitoAuthServiceTests()
    {
        _cognitoMock = Substitute.For<IAmazonCognitoIdentityProvider>();

        var inMemorySettings = new Dictionary<string, string?> {
            {"ASPNETCORE_ENVIRONMENT", "Development"},
            {"AWS:UserPoolId", "us-east-1_testpool"},
            {"AWS:ClientId", "testclient"}
        };
        _configMock = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task RegisterAsync_ShouldRegisterSuccessfully_WhenCognitoCallSucceeds()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _configMock);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SignUpResponse { UserSub = "sub-123" });

        _cognitoMock.AdminConfirmSignUpAsync(Arg.Any<AdminConfirmSignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminConfirmSignUpResponse());

        // Act
        var result = await service.RegisterAsync("neto@email.com", "Senha123", "Neto");

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("sub-123");
        result.Email.Should().Be("neto@email.com");
        result.Name.Should().Be("Neto");
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowEmailAlreadyExistsException_WhenCognitoThrowsUsernameExistsException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _configMock);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SignUpResponse>(new UsernameExistsException("User already exists")));

        // Act
        Func<Task> act = async () => await service.RegisterAsync("neto@email.com", "Senha123", "Neto");

        // Assert
        await act.Should().ThrowAsync<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldLoginSuccessfully_WhenCognitoCallSucceeds()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _configMock);

        var authResponse = new InitiateAuthResponse
        {
            AuthenticationResult = new AuthenticationResultType
            {
                AccessToken = "access-token-123",
                ExpiresIn = 3600
            }
        };

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(authResponse);

        var getUserResponse = new GetUserResponse
        {
            Username = "sub-123",
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "sub", Value = "sub-123" },
                new() { Name = "name", Value = "Neto" }
            }
        };

        _cognitoMock.GetUserAsync(Arg.Any<GetUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(getUserResponse);

        // Act
        var result = await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token-123");
        result.ExpiresIn.Should().Be(3600);
        result.UserId.Should().Be("sub-123");
        result.Name.Should().Be("Neto");
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WhenCognitoThrowsNotAuthorizedException()
    {
        // Arrange
        var service = new CognitoAuthService(_cognitoMock, _configMock);

        _cognitoMock.InitiateAuthAsync(Arg.Any<InitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<InitiateAuthResponse>(new NotAuthorizedException("Invalid credentials")));

        // Act
        Func<Task> act = async () => await service.LoginAsync("neto@email.com", "Senha123");

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task RegisterAsync_ShouldAutoDiscoverUserPoolAndClient_WhenConfigIsEmpty()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                {"ASPNETCORE_ENVIRONMENT", "Development"}
            })
            .Build();

        var service = new CognitoAuthService(_cognitoMock, emptyConfig);

        var listPoolsResponse = new ListUserPoolsResponse
        {
            UserPools = new List<UserPoolDescriptionType>
            {
                new() { Name = "GastosAppUserPool", Id = "us-east-1_discovered" }
            }
        };
        _cognitoMock.ListUserPoolsAsync(Arg.Any<ListUserPoolsRequest>(), Arg.Any<CancellationToken>())
            .Returns(listPoolsResponse);

        var listClientsResponse = new ListUserPoolClientsResponse
        {
            UserPoolClients = new List<UserPoolClientDescription>
            {
                new() { ClientName = "GastosAppClient", ClientId = "client-discovered" }
            }
        };
        _cognitoMock.ListUserPoolClientsAsync(Arg.Any<ListUserPoolClientsRequest>(), Arg.Any<CancellationToken>())
            .Returns(listClientsResponse);

        _cognitoMock.SignUpAsync(Arg.Any<SignUpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SignUpResponse { UserSub = "sub-123" });

        // Act
        var result = await service.RegisterAsync("neto@email.com", "Senha123", "Neto");

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("sub-123");
        await _cognitoMock.Received(1).ListUserPoolsAsync(Arg.Any<ListUserPoolsRequest>(), Arg.Any<CancellationToken>());
        await _cognitoMock.Received(1).ListUserPoolClientsAsync(Arg.Any<ListUserPoolClientsRequest>(), Arg.Any<CancellationToken>());
    }
}
