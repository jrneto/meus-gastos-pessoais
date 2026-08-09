using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using GastosApp.Application.Auth;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
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

    public async Task<Result<RegisterResult>> RegisterAsync(
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

            return Result.Success(new RegisterResult(response.UserSub, email));
        }
        catch (UsernameExistsException)
        {
            return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
        }
    }

    public async Task<Result<LoginResult>> LoginAsync(
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

            return Result.Success(new LoginResult(result.IdToken, result.ExpiresIn ?? 3600, userId, result.RefreshToken));
        }
        catch (NotAuthorizedException)
        {
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }
        catch (UserNotFoundException)
        {
            return Result.Failure<LoginResult>(AuthErrors.InvalidCredentials);
        }
    }

    public async Task<Result<RefreshResult>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var authResponse = await _cognitoClient.InitiateAuthAsync(new InitiateAuthRequest
            {
                AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                ClientId = _options.ClientId,
                AuthParameters = new Dictionary<string, string>
                {
                    ["REFRESH_TOKEN"] = refreshToken
                }
            }, cancellationToken);

            var result = authResponse.AuthenticationResult;

            var userResponse = await _cognitoClient.GetUserAsync(new GetUserRequest
            {
                AccessToken = result.AccessToken
            }, cancellationToken);

            var userId = userResponse.UserAttributes.FirstOrDefault(a => a.Name == "sub")?.Value ?? userResponse.Username;

            return Result.Success(new RefreshResult(result.IdToken, result.ExpiresIn ?? 3600, userId));
        }
        catch (NotAuthorizedException)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }
    }
}