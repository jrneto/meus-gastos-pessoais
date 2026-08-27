using FluentValidation;
using GastosApp.Domain.Users;

namespace GastosApp.Application.Auth.Commands.Register;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 150;

    public RegisterUserCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");

        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .Must(name => name.Trim().Length is >= MinNameLength and <= MaxNameLength)
                .WithMessage($"Nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres.");

        RuleFor(c => c.PhoneNumber)
            .NotEmpty().WithMessage("Telefone é obrigatório.")
            .Must(phone => phone.Length is 10 or 11 && phone.All(char.IsDigit))
                .WithMessage("Telefone deve conter 10 ou 11 dígitos numéricos.");

        RuleFor(c => c.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(cpf => cpf.Length == 11 && cpf.All(char.IsDigit))
                .WithMessage("CPF deve conter 11 dígitos numéricos.")
            .Must(Cpf.IsValid).WithMessage("CPF inválido.");
    }
}
