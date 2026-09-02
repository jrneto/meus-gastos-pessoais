using FluentValidation;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand<Result>;

public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken) =>
        new(_authService.ForgotPasswordAsync(command.Email, cancellationToken));
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
    }
}
