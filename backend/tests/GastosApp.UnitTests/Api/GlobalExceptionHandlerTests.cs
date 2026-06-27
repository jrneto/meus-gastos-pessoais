using System.IO;
using System.Text.Json;
using FluentAssertions;
using GastosApp.Api.Middlewares;
using GastosApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Api;

public class GlobalExceptionHandlerTests
{
    private readonly ILogger<GlobalExceptionHandler> _loggerMock;
    private readonly GlobalExceptionHandler _handler;
    private readonly DefaultHttpContext _context;

    public GlobalExceptionHandlerTests()
    {
        _loggerMock = Substitute.For<ILogger<GlobalExceptionHandler>>();
        _handler = new GlobalExceptionHandler(_loggerMock);
        _context = new DefaultHttpContext();
        _context.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task TryHandleAsync_ShouldReturn409ProblemDetails_WhenExceptionIsEmailAlreadyExistsException()
    {
        // Arrange
        var exception = new EmailAlreadyExistsException("Email já cadastrado");

        // Act
        var result = await _handler.TryHandleAsync(_context, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        _context.Response.ContentType.Should().Be("application/problem+json");

        var problemDetails = await ReadProblemDetailsAsync();
        problemDetails.Status.Should().Be(StatusCodes.Status409Conflict);
        problemDetails.Title.Should().Be("Email já cadastrado");
        problemDetails.Type.Should().Be("https://gastosapp.dev/errors/email-already-exists");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldReturn401ProblemDetails_WhenExceptionIsInvalidCredentialsException()
    {
        // Arrange
        var exception = new InvalidCredentialsException("Email ou senha inválidos");

        // Act
        var result = await _handler.TryHandleAsync(_context, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _context.Response.ContentType.Should().Be("application/problem+json");

        var problemDetails = await ReadProblemDetailsAsync();
        problemDetails.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problemDetails.Title.Should().Be("Email ou senha inválidos");
        problemDetails.Type.Should().Be("https://gastosapp.dev/errors/invalid-credentials");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldReturn400ProblemDetails_WhenExceptionIsArgumentException()
    {
        // Arrange
        var exception = new ArgumentException("Parâmetros inválidos");

        // Act
        var result = await _handler.TryHandleAsync(_context, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _context.Response.ContentType.Should().Be("application/problem+json");

        var problemDetails = await ReadProblemDetailsAsync();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Parâmetros inválidos");
        problemDetails.Detail.Should().Be("Parâmetros inválidos");
        problemDetails.Type.Should().Be("https://gastosapp.dev/errors/bad-request");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldReturn500ProblemDetails_WhenExceptionIsGenericException()
    {
        // Arrange
        var exception = new Exception("Erro catastrófico");

        // Act
        var result = await _handler.TryHandleAsync(_context, exception, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        _context.Response.ContentType.Should().Be("application/problem+json");

        var problemDetails = await ReadProblemDetailsAsync();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Erro interno do servidor");
        problemDetails.Type.Should().Be("https://gastosapp.dev/errors/internal-server-error");
    }

    private async Task<ProblemDetails> ReadProblemDetailsAsync()
    {
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
