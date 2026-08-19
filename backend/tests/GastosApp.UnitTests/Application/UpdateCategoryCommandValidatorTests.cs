using FluentAssertions;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldBeValid_WhenCommandIsValid()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "#0EA5E9", "plane");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenNomeIsEmpty(string nome)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", nome, "#0EA5E9", "plane");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNomeExceedsMaxLength()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", new string('a', 51), "#0EA5E9", "plane");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenNomeHasNoAlphanumericCharacters()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "!!!", "#0EA5E9", "plane");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("0EA5E9")]
    [InlineData("#GGGGGG")]
    public void Validate_ShouldBeInvalid_WhenCorIsNotAValidHexColor(string cor)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", cor, "plane");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldBeInvalid_WhenIconeIsEmpty(string icone)
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "#0EA5E9", icone);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldBeInvalid_WhenIconeExceedsMaxLength()
    {
        var command = new UpdateCategoryCommand("user-id-123", "category-1", "Viagens", "#0EA5E9", new string('a', 51));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}
