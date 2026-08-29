using FluentAssertions;
using GastosApp.Application.Members.Commands.InviteMember;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class InviteMemberCommandValidatorTests
{
    private readonly InviteMemberCommandValidator _validator = new();

    [Theory]
    [InlineData("", "Leitura")]
    [InlineData("   ", "Leitura")]
    [InlineData("nao-e-email", "Leitura")]
    public async Task Validate_ShouldFail_WhenEmailIsInvalid(string email, string role)
    {
        var result = await _validator.ValidateAsync(new InviteMemberCommand("account-1", email, role));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Titular")]
    [InlineData("Admin")]
    public async Task Validate_ShouldFail_WhenRoleIsInvalid(string role)
    {
        var result = await _validator.ValidateAsync(new InviteMemberCommand("account-1", "convidado@email.com", role));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Leitura")]
    [InlineData("Lancar")]
    [InlineData("Total")]
    public async Task Validate_ShouldSucceed_WhenEmailAndRoleAreValid(string role)
    {
        var result = await _validator.ValidateAsync(new InviteMemberCommand("account-1", "convidado@email.com", role));

        result.IsValid.Should().BeTrue();
    }
}
