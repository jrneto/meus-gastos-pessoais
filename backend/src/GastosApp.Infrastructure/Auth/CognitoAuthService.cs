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
        string email, string password, string name,
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
                    new AttributeType { Name = "email", Value = email },
                    new AttributeType { Name = "name", Value = name }
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
        catch (UserNotConfirmedException)
        {
            return Result.Failure<LoginResult>(AuthErrors.UserNotConfirmed);
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

    public async Task DeleteAsync(string email, CancellationToken cancellationToken = default)
    {
        // Username = email porque o User Pool usa username_attributes=["email"]
        // (cognito.tf) — não é um alias, é o próprio Username.
        await _cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _options.UserPoolId,
            Username = email
        }, cancellationToken);
    }

    public async Task<Result> ConfirmSignUpAsync(
        string email, string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cognitoClient.ConfirmSignUpAsync(new ConfirmSignUpRequest
            {
                ClientId = _options.ClientId,
                Username = email,
                ConfirmationCode = code
            }, cancellationToken);

            return Result.Success();
        }
        catch (ExpiredCodeException)
        {
            return Result.Failure(AuthErrors.ExpiredConfirmationCode);
        }
        catch (CodeMismatchException)
        {
            return Result.Failure(AuthErrors.InvalidConfirmationCode);
        }
        catch (UserNotFoundException)
        {
            // Mesma resposta de código incorreto — não revela se o email
            // está cadastrado (spec.md, decisão 1).
            return Result.Failure(AuthErrors.InvalidConfirmationCode);
        }
        catch (NotAuthorizedException)
        {
            // Cognito recusa ConfirmSignUp de usuário já confirmado com
            // "User cannot be confirmed. Current status is CONFIRMED" —
            // único cenário realista de NotAuthorizedException aqui (não há
            // senha/token envolvido). Idempotente, não é erro (spec.md,
            // decisão 2).
            return Result.Success();
        }
    }

    public async Task<Result> ResendConfirmationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cognitoClient.ResendConfirmationCodeAsync(new ResendConfirmationCodeRequest
            {
                ClientId = _options.ClientId,
                Username = email
            }, cancellationToken);
        }
        catch (UserNotFoundException)
        {
            // Não revela se o email existe (spec.md, decisão 3).
        }
        catch (InvalidParameterException)
        {
            // Cognito recusa reenvio pra usuário já confirmado com esse tipo
            // de exceção — mesmo princípio de não-enumeração.
        }

        // Sempre 200 (spec.md, decisão 3). Qualquer exceção fora das duas
        // acima (ex.: LimitExceededException do throttling nativo do
        // Cognito) é verdadeiramente inesperada e propaga pro
        // GlobalExceptionHandler (500), igual ao resto da API.
        return Result.Success();
    }
}