using FluentAssertions;
using GastosApp.Application.Auth.Commands.Register;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterUserCommandValidatorTests
{
    private const string ValidEmail = "neto@email.com";
    private const string ValidPassword = "Senha123";
    private const string ValidName = "Fulano da Silva";
    private const string ValidPhoneNumber = "11999998888";
    private const string ValidCpf = "11144477735";

    private readonly RegisterUserCommandValidator _validator = new();

    private static RegisterUserCommand ValidCommand(
        string email = ValidEmail, string password = ValidPassword, string name = ValidName,
        string phoneNumber = ValidPhoneNumber, string cpf = ValidCpf) =>
        new(email, password, name, phoneNumber, cpf);

    [Fact]
    public void Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var result = _validator.Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenEmailIsEmpty(string email)
    {
        var result = _validator.Validate(ValidCommand(email: email));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    public void Validate_ShouldBeInvalid_WhenPasswordIsEmptyOrTooShort(string password)
    {
        var result = _validator.Validate(ValidCommand(password: password));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Validate_ShouldBeInvalid_WhenNameIsEmptyOrTooShort(string name)
    {
        var result = _validator.Validate(ValidCommand(name: name));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNameExceedsMaxLength()
    {
        var result = _validator.Validate(ValidCommand(name: new string('A', 151)));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("(11) 99999-8888")]
    [InlineData("+5511999998888")]
    [InlineData("119999988")]     // 9 dígitos
    [InlineData("119999988889")]  // 12 dígitos
    public void Validate_ShouldBeInvalid_WhenPhoneNumberIsInvalid(string phoneNumber)
    {
        var result = _validator.Validate(ValidCommand(phoneNumber: phoneNumber));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("1199999888")]  // 10 dígitos (fixo)
    [InlineData("11999998888")] // 11 dígitos (celular)
    public void Validate_ShouldBeValid_WhenPhoneNumberHas10Or11Digits(string phoneNumber)
    {
        var result = _validator.Validate(ValidCommand(phoneNumber: phoneNumber));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("111.444.777-35")]
    [InlineData("1234567890")]    // 10 dígitos
    [InlineData("123456789012")]  // 12 dígitos
    [InlineData("11111111111")]   // dígitos repetidos
    [InlineData("11144477736")]   // dígito verificador errado
    public void Validate_ShouldBeInvalid_WhenCpfIsInvalid(string cpf)
    {
        var result = _validator.Validate(ValidCommand(cpf: cpf));

        result.IsValid.Should().BeFalse();
    }
}
