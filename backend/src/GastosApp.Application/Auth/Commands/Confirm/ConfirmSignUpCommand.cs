using FluentValidation;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.Confirm;

public sealed record ConfirmSignUpCommand(string Email, string Code) : ICommand<Result>;

public sealed class ConfirmSignUpCommandHandler : ICommandHandler<ConfirmSignUpCommand, Result>
{
    private readonly IAuthService _authService;

    public ConfirmSignUpCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ConfirmSignUpCommand command, CancellationToken cancellationToken) =>
        new(_authService.ConfirmSignUpAsync(command.Email, command.Code, cancellationToken));
}

public sealed class ConfirmSignUpCommandValidator : AbstractValidator<ConfirmSignUpCommand>
{
    public ConfirmSignUpCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
        RuleFor(c => c.Code).NotEmpty().WithMessage("Código de confirmação é obrigatório.");
    }
}
