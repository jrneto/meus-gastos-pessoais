using System.Security.Claims;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Register;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace GastosApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            [FromServices] RegisterUserCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RegisterUserCommand(request.Email, request.Password, request.Name);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created($"/auth/me", result);
        });

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            [FromServices] LoginUserCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new LoginUserCommand(request.Email, request.Password);
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/me", (ClaimsPrincipal user) =>
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
        })
        .RequireAuthorization();
    }
}

public record RegisterRequest(string Email, string Password, string Name);
public record LoginRequest(string Email, string Password);
