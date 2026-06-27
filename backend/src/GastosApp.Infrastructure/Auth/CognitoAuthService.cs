using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GastosApp.Infrastructure.Auth;

public class CognitoAuthService : IAuthService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly IConfiguration _configuration;
    private string? _userPoolId;
    private string? _clientId;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CognitoAuthService(IAmazonCognitoIdentityProvider cognitoClient, IConfiguration configuration)
    {
        _cognitoClient = cognitoClient;
        _configuration = configuration;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_userPoolId != null && _clientId != null) return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_userPoolId != null && _clientId != null) return;

            _userPoolId = _configuration["AWS:UserPoolId"];
            _clientId = _configuration["AWS:ClientId"];

            var isDev = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            if (isDev && (string.IsNullOrEmpty(_userPoolId) || string.IsNullOrEmpty(_clientId)))
            {
                var listUserPoolsResponse = await _cognitoClient.ListUserPoolsAsync(new ListUserPoolsRequest { MaxResults = 60 }, cancellationToken);
                var userPool = listUserPoolsResponse.UserPools.FirstOrDefault(p => p.Name == "GastosAppUserPool");
                if (userPool != null)
                {
                    _userPoolId = userPool.Id;

                    var listClientsResponse = await _cognitoClient.ListUserPoolClientsAsync(new ListUserPoolClientsRequest
                    {
                        UserPoolId = _userPoolId,
                        MaxResults = 60
                    }, cancellationToken);

                    var client = listClientsResponse.UserPoolClients.FirstOrDefault(c => c.ClientName == "GastosAppClient")
                                 ?? listClientsResponse.UserPoolClients.FirstOrDefault();

                    if (client != null)
                    {
                        _clientId = client.ClientId;
                    }
                    else
                    {
                        var createClientResponse = await _cognitoClient.CreateUserPoolClientAsync(new CreateUserPoolClientRequest
                        {
                            UserPoolId = _userPoolId,
                            ClientName = "GastosAppClient",
                            ExplicitAuthFlows = new List<string> { "USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH", "ALLOW_CUSTOM_AUTH" }
                        }, cancellationToken);
                        _clientId = createClientResponse.UserPoolClient.ClientId;
                    }
                }
            }

            if (string.IsNullOrEmpty(_userPoolId))
                throw new InvalidOperationException("Cognito UserPoolId is not configured or could not be auto-discovered.");
            if (string.IsNullOrEmpty(_clientId))
                throw new InvalidOperationException("Cognito ClientId is not configured or could not be auto-discovered.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var signUpRequest = new SignUpRequest
            {
                ClientId = _clientId,
                Username = email,
                Password = password,
                UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "email", Value = email },
                    new AttributeType { Name = "name", Value = name }
                }
            };

            var signUpResponse = await _cognitoClient.SignUpAsync(signUpRequest, cancellationToken);
            var userId = signUpResponse.UserSub;

            try
            {
                await _cognitoClient.AdminConfirmSignUpAsync(new AdminConfirmSignUpRequest
                {
                    UserPoolId = _userPoolId,
                    Username = email
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Confirm sign up warning: {ex.Message}");
            }

            return new RegisterResult(userId, email, name);
        }
        catch (UsernameExistsException)
        {
            throw new EmailAlreadyExistsException();
        }
        catch (Exception ex) when (ex is not EmailAlreadyExistsException)
        {
            Console.WriteLine($"Error registering user in Cognito: {ex.Message}");
            throw;
        }
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var authRequest = new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = _clientId,
                AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", email },
                    { "PASSWORD", password }
                }
            };

            var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest, cancellationToken);

            string name = string.Empty;
            string userId = string.Empty;
            try
            {
                var getUserResponse = await _cognitoClient.GetUserAsync(new GetUserRequest
                {
                    AccessToken = authResponse.AuthenticationResult.AccessToken
                }, cancellationToken);

                name = getUserResponse.UserAttributes.FirstOrDefault(a => a.Name == "name")?.Value ?? string.Empty;
                userId = getUserResponse.UserAttributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? getUserResponse.Username;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get user warning: {ex.Message}");
            }

            return new LoginResult(
                authResponse.AuthenticationResult.AccessToken,
                authResponse.AuthenticationResult.ExpiresIn ?? 3600,
                userId,
                name
            );
        }
        catch (NotAuthorizedException)
        {
            throw new InvalidCredentialsException();
        }
        catch (UserNotFoundException)
        {
            throw new InvalidCredentialsException();
        }
        catch (Exception ex) when (ex is not InvalidCredentialsException)
        {
            Console.WriteLine($"Error authenticating user in Cognito: {ex.Message}");
            throw;
        }
    }
}
