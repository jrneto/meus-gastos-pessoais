using GastosApp.Api.Common;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Logout;
using GastosApp.Application.Auth.Commands.Refresh;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Auth.Queries.GetCurrentUser;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GastosApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/register", RegisterUser)
            .Produces<RegisterUserResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", Login)
            .Produces<LoginUserResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", Refresh)
            .Produces<RefreshTokenResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/me", UserData)
            .RequireAuthorization()
            .Produces<UserInfoResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> UserData(ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {

        var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Json(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Não autorizado",
                Type = "https://gastosapp.dev/errors/unauthorized"
            }, AppJsonSerializerContext.Default.ProblemDetails, statusCode: StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
        }

        var query = new GetCurrentUserQuery(userId, email);
        var result = await sender.Send(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(new UserInfoResponse(value.UserId, value.Email, value.Name, value.PhoneNumber, value.Cpf)));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value =>
        {
            httpContext.Response.Cookies.Append(RefreshTokenCookie.Name, value.RefreshToken, RefreshTokenCookie.ForSet());
            return Results.Ok(value);
        });
    }

    private static async Task<IResult> Refresh(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var refreshToken = httpContext.Request.Cookies[RefreshTokenCookie.Name];
        var command = new RefreshTokenCommand(refreshToken ?? string.Empty);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            httpContext.Response.Cookies.Append(RefreshTokenCookie.Name, string.Empty, RefreshTokenCookie.ForClear());

        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> Logout(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(), cancellationToken);
        httpContext.Response.Cookies.Append(RefreshTokenCookie.Name, string.Empty, RefreshTokenCookie.ForClear());
        return Results.Ok();
    }

    private static async Task<IResult> RegisterUser(
        RegisterRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.Password, request.Name, request.PhoneNumber, request.Cpf);
        var result = await sender.Send(command, cancellationToken);
        return result.ToHttpResult(value => Results.Created("/auth/me", value));
    }
}

public record RegisterRequest(string Email, string Password, string Name, string PhoneNumber, string Cpf);
public record LoginRequest(string Email, string Password);
public record UserInfoResponse(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf);
