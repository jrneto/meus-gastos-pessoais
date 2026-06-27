using GastosApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GastosApp.Api.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Erro na execução da requisição: {Message}", exception.Message);

        var problemDetails = new ProblemDetails();

        switch (exception)
        {
            case EmailAlreadyExistsException:
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Email já cadastrado";
                problemDetails.Type = "https://gastosapp.dev/errors/email-already-exists";
                break;

            case InvalidCredentialsException:
                problemDetails.Status = StatusCodes.Status401Unauthorized;
                problemDetails.Title = "Email ou senha inválidos";
                problemDetails.Type = "https://gastosapp.dev/errors/invalid-credentials";
                break;

            case ArgumentException argumentEx:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Parâmetros inválidos";
                problemDetails.Detail = argumentEx.Message;
                problemDetails.Type = "https://gastosapp.dev/errors/bad-request";
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "Erro interno do servidor";
                problemDetails.Type = "https://gastosapp.dev/errors/internal-server-error";
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
