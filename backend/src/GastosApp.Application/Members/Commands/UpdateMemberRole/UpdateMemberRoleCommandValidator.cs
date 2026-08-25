using FluentValidation;

namespace GastosApp.Application.Members.Commands.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleCommandValidator()
    {
        RuleFor(c => c.Role)
            .NotEmpty().WithMessage("Papel de acesso é obrigatório.")
            .Must(role => role is "Leitura" or "Lancar" or "Total")
                .WithMessage("Papel de acesso inválido.");
    }
}
