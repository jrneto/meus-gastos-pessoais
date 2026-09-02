using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Domain.Users;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Register;

public sealed record RegisterUserCommand(string Email, string Password, string Name, string PhoneNumber, string Cpf)
    : ICommand<Result<RegisterUserResult>>;

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    private readonly IAuthService _authService;
    private readonly IUserProfileRepository _userProfileRepository;

    public RegisterUserCommandHandler(IAuthService authService, IUserProfileRepository userProfileRepository)
    {
        _authService = authService;
        _userProfileRepository = userProfileRepository;
    }

    public async ValueTask<Result<RegisterUserResult>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        // Validação de formato (email/password/name/phoneNumber/cpf) já rodou no
        // ValidationBehavior via RegisterUserCommandValidator — Handle fica só com
        // orquestração (constitution: "Handlers não devem conter validação manual").
        var authResult = await _authService.RegisterAsync(command.Email, command.Password, command.Name.Trim(), cancellationToken);
        if (authResult.IsFailure)
            return Result.Failure<RegisterUserResult>(authResult.Error!);

        var profile = UserProfile.Create(authResult.Value.UserId, command.Name.Trim(), command.PhoneNumber, command.Cpf);

        CreateUserProfileResult profileResult;
        try
        {
            profileResult = await _userProfileRepository.CreateAsync(profile, cancellationToken);
        }
        catch
        {
            // Falha inesperada (ex.: throttling) gravando o perfil — desfaz o SignUp
            // pra não deixar conta "pela metade" (spec.md, US8). Não vira
            // Result.Failure: não é um outcome de negócio esperado, segue pro
            // GlobalExceptionHandler (500) depois do rollback.
            await _authService.DeleteAsync(command.Email, cancellationToken);
            throw;
        }

        if (profileResult.CpfAlreadyExists)
        {
            // CPF em conflito É um outcome de negócio esperado (409), mas o SignUp já
            // aconteceu — desfaz do mesmo jeito, senão o e-mail fica "queimado" no
            // Cognito e uma nova tentativa (CPF corrigido) esbarra em
            // email-already-exists em vez do erro real.
            await _authService.DeleteAsync(command.Email, cancellationToken);
            return Result.Failure<RegisterUserResult>(AuthErrors.CpfAlreadyExists);
        }

        return Result.Success(RegisterUserResult.FromEntity(authResult.Value, profile));
    }
}

public sealed record RegisterUserResult(string UserId, string Email, string Name, string PhoneNumber, string Cpf)
{
    public static RegisterUserResult FromEntity(RegisterResult authResult, UserProfile profile) =>
        new(authResult.UserId, authResult.Email, profile.Name, profile.PhoneNumber, profile.Cpf);
}
