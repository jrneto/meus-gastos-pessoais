using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using GastosApp.Application.Common.Exceptions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace GastosApp.Infrastructure.Auth;

public sealed class CognitoAuthService : IAuthService
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;
    private readonly CognitoOptions _options;

    public CognitoAuthService(
        IAmazonCognitoIdentityProvider cognitoClient,
        IOptions<CognitoOptions> options)
    {
        _cognitoClient = cognitoClient;
        _options = options.Value;
    }

    public async Task<RegisterResult> RegisterAsync(
        string email, string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cognitoClient.SignUpAsync(new SignUpRequest
            {
                ClientId = _options.ClientId,
                Username = email,
                Password = password,
                UserAttributes =
                [
                    new AttributeType { Name = "email", Value = email }
                ]
            }, cancellationToken);

            return new RegisterResult(response.UserSub, email);
        }
        catch (UsernameExistsException)
        {
            throw new EmailAlreadyExistsException();
        }
    }

    public async Task<LoginResult> LoginAsync(
        string email, string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authResponse = await _cognitoClient.InitiateAuthAsync(new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                ClientId = _options.ClientId,
                AuthParameters = new Dictionary<string, string>
                {
                    ["USERNAME"] = email,
                    ["PASSWORD"] = password
                }
            }, cancellationToken);

            var result = authResponse.AuthenticationResult;

            var userResponse = await _cognitoClient.GetUserAsync(new GetUserRequest
            {
                AccessToken = result.AccessToken
            }, cancellationToken);

            var name = userResponse.UserAttributes.FirstOrDefault(a => a.Name == "name")?.Value ?? string.Empty;
            var userId = userResponse.UserAttributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? userResponse.Username;

            return new LoginResult(result.IdToken, result.ExpiresIn ?? 3600, userId);
        }
        catch (NotAuthorizedException)
        {
            throw new InvalidCredentialsException();
        }
        catch (UserNotFoundException)
        {
            throw new InvalidCredentialsException();
        }
    }
}