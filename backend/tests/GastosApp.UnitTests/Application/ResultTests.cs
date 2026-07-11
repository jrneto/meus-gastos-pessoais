using FluentAssertions;
using GastosApp.Application.Common.Results;

namespace GastosApp.UnitTests.Application;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        var error = Error.Validation("bad-request", "Campo obrigatório.");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void GenericSuccess_ShouldExposeValue()
    {
        var result = Result<string>.Success("valor");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("valor");
    }

    [Fact]
    public void GenericFailure_ShouldThrow_WhenAccessingValue()
    {
        var error = Error.Conflict("email-already-exists", "Email já cadastrado");
        var result = Result<string>.Failure(error);

        result.IsFailure.Should().BeTrue();
        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ImplicitConversion_ShouldWrapValueAsSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }
}
