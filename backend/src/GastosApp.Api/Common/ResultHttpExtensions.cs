using GastosApp.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace GastosApp.Api.Common;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess(result.Value) : BuildProblem(result.Error!);
    }

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess() : BuildProblem(result.Error!);
    }

    private static IResult BuildProblem(Error error)
    {
        var (status, title, detail) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Parâmetros inválidos", error.Message),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, error.Message, (string?)null),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, error.Message, (string?)null),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, error.Message, (string?)null),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor", (string?)null)
        };

        var type = error.Type == ErrorType.Failure
            ? "https://gastosapp.dev/errors/internal-server-error"
            : $"https://gastosapp.dev/errors/{error.Code}";

        return Results.Json(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = type
            },
            statusCode: status,
            contentType: "application/problem+json");
    }
}
