using GastosApp.Api.Common;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Register;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GastosApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/register", RegisterUser);

        group.MapPost("/login", Login);

        group.MapGet("/me", UserData)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UserData(ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {

        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
        var name = user.FindFirst("name")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Json(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Type = "https://gastosapp.dev/errors/unauthorized"
            }, statusCode: StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        }

        return Results.Ok(new { userId, email, name });

    }

    private static async Task<IResult> Login(
        LoginRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> RegisterUser(
        RegisterRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created("/auth/me", value));
    }
}

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
