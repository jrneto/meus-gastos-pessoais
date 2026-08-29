using FluentValidation;

namespace GastosApp.Application.Members.Commands.InviteMember;

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail em formato inválido.");

        RuleFor(c => c.Role)
            .NotEmpty().WithMessage("Papel de acesso é obrigatório.")
            .Must(role => role is "Leitura" or "Lancar" or "Total")
                .WithMessage("Papel de acesso inválido.");
    }
}
