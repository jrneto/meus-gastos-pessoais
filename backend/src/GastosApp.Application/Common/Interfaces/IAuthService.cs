using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<RegisterResult>> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default);
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<RefreshResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    // Rollback de um SignUp já concluído (FEAT-26) — usado quando a gravação do
    // perfil (nome/telefone/cpf) falha depois do Cognito já ter criado o usuário,
    // pra não deixar uma conta "pela metade" nem "queimar" o email numa tentativa
    // frustrada de cadastro.
    Task DeleteAsync(string email, CancellationToken cancellationToken = default);

    // FEAT-35: confirmação de cadastro via código OTP enviado por email.
    Task<Result> ConfirmSignUpAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<Result> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default);
}

public record RegisterResult(string UserId, string Email);
public record LoginResult(string AccessToken, int ExpiresIn, string UserId, string RefreshToken);
public record RefreshResult(string AccessToken, int ExpiresIn, string UserId);
