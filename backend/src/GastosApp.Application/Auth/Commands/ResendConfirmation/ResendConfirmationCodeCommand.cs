using FluentValidation;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Auth.Commands.ResendConfirmation;

public sealed record ResendConfirmationCodeCommand(string Email) : ICommand<Result>;

public sealed class ResendConfirmationCodeCommandHandler : ICommandHandler<ResendConfirmationCodeCommand, Result>
{
    private readonly IAuthService _authService;

    public ResendConfirmationCodeCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ResendConfirmationCodeCommand command, CancellationToken cancellationToken) =>
        new(_authService.ResendConfirmationCodeAsync(command.Email, cancellationToken));
}

public sealed class ResendConfirmationCodeCommandValidator : AbstractValidator<ResendConfirmationCodeCommand>
{
    public ResendConfirmationCodeCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
    }
}
