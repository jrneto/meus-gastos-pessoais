using FluentAssertions;
using GastosApp.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

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
    public async Task TryHandleAsync_ShouldReturn500ProblemDetails_WhenExceptionIsNotHandledElsewhere()
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
