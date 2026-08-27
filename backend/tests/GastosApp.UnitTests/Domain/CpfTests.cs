using FluentAssertions;
using GastosApp.Domain.Users;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class CpfTests
{
    [Fact]
    public void IsValid_ShouldReturnTrue_ForKnownValidCpf()
    {
        Cpf.IsValid("11144477735").Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenCheckDigitIsWrong()
    {
        Cpf.IsValid("11144477736").Should().BeFalse();
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void IsValid_ShouldReturnFalse_ForRepeatedDigitSequences(string cpf)
    {
        Cpf.IsValid(cpf).Should().BeFalse();
    }

    [Theory]
    [InlineData("1234567890")]   // 10 dígitos
    [InlineData("123456789012")] // 12 dígitos
    [InlineData("")]
    public void IsValid_ShouldReturnFalse_WhenLengthIsNot11(string cpf)
    {
        Cpf.IsValid(cpf).Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenContainsNonDigitCharacters()
    {
        Cpf.IsValid("111.444.777-35").Should().BeFalse();
    }
}
