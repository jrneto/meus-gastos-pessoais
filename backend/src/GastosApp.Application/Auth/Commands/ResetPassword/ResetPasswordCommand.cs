using FluentValidation;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using Mediator;
using Microsoft.Extensions.Logging;

namespace GastosApp.Application.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword, string? UserAgent) : ICommand<Result>;

public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private readonly IAuthService _authService;
    private readonly IPasswordChangedEmailSender _emailSender;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IAuthService authService,
        IPasswordChangedEmailSender emailSender,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _authService = authService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async ValueTask<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmForgotPasswordAsync(
            command.Email, command.Code, command.NewPassword, cancellationToken);

        if (result.IsFailure)
            return result;

        try
        {
            await _emailSender.SendAsync(command.Email, command.UserAgent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nunca propaga: a senha já foi trocada de fato no Cognito
            // (spec.md) — falha no envio deste email de aviso não pode
            // derrubar a resposta de sucesso. Mesma filosofia defensiva do
            // AccountTriggerHandler (FEAT-19).
            _logger.LogError(ex, "Falha ao enviar email de senha alterada para {Email}.", command.Email);
        }

        return Result.Success();
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
        RuleFor(c => c.Code).NotEmpty().WithMessage("Código de recuperação é obrigatório.");
        RuleFor(c => c.NewPassword).NotEmpty().WithMessage("Nova senha é obrigatória.");
    }
}
