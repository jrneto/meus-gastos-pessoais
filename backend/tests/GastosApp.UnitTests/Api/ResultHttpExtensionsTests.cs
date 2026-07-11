using FluentAssertions;
using GastosApp.Api.Common;
using GastosApp.Application.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GastosApp.UnitTests.Api;

public class ResultHttpExtensionsTests
{
    [Fact]
    public void ToHttpResult_ShouldReturnOnSuccessResult_WhenResultIsSuccess()
    {
        var result = Result<string>.Success("valor");

        var httpResult = result.ToHttpResult(Results.Ok);

        httpResult.Should().BeOfType<Ok<string>>();
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest, "Parâmetros inválidos")]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, "Email ou senha inválidos")]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict, "Email já cadastrado")]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound, "Não encontrado")]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError, "Erro interno do servidor")]
    public void ToHttpResult_ShouldMapErrorTypeToExpectedStatusAndTitle(ErrorType errorType, int expectedStatus, string expectedTitle)
    {
        // Para Validation/Failure o título é fixo; para os demais, o título é a própria mensagem do erro.
        var error = new Error("some-code", expectedTitle, errorType);
        var result = Result<string>.Failure(error);

        var httpResult = result.ToHttpResult(Results.Ok);

        var jsonResult = httpResult.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        jsonResult.StatusCode.Should().Be(expectedStatus);

        var problemDetails = ((JsonHttpResult<ProblemDetails>)httpResult).Value!;
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void ToHttpResult_ShouldUseInternalServerErrorType_ForFailureErrorType()
    {
        var error = Error.Failure("infra-error", "Detalhe interno não deve vazar");
        var result = Result<string>.Failure(error);

        var httpResult = (JsonHttpResult<ProblemDetails>)result.ToHttpResult(Results.Ok);

        httpResult.Value!.Type.Should().Be("https://gastosapp.dev/errors/internal-server-error");
        httpResult.Value.Detail.Should().BeNull();
    }

    [Fact]
    public void ToHttpResult_ShouldUseErrorCodeInTypeUri_ForNonFailureErrors()
    {
        var error = AuthErrorLike();
        var result = Result<string>.Failure(error);

        var httpResult = (JsonHttpResult<ProblemDetails>)result.ToHttpResult(Results.Ok);

        httpResult.Value!.Type.Should().Be("https://gastosapp.dev/errors/email-already-exists");
    }

    private static Error AuthErrorLike() => Error.Conflict("email-already-exists", "Email já cadastrado");
}
