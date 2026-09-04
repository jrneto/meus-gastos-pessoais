using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Infrastructure.Email;
using NSubstitute;

namespace GastosApp.UnitTests.Infrastructure;

public class SesPasswordChangedEmailSenderTests
{
    private readonly IEmailSender _emailSenderMock;

    public SesPasswordChangedEmailSenderTests()
    {
        _emailSenderMock = Substitute.For<IEmailSender>();
    }

    [Fact]
    public async Task SendAsync_ShouldCallEmailSender_WithSubjectAndFilledTemplate()
    {
        // Arrange
        var sender = new SesPasswordChangedEmailSender(_emailSenderMock);

        // Act
        await sender.SendAsync("neto@email.com", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        // Assert
        await _emailSenderMock.Received(1).SendAsync(
            "neto@email.com",
            "Sua senha foi alterada — jrn.expenses",
            Arg.Is<string>(html =>
                html.Contains("A senha da conta neto@email.com foi redefinida com sucesso.") &&
                html.Contains("Mozilla/5.0 (Windows NT 10.0; Win64; x64)") &&
                !html.Contains("{{email}}") &&
                !html.Contains("{{data}}") &&
                !html.Contains("{{dispositivo}}") &&
                !html.Contains("{{nome}}")),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendAsync_ShouldUseFallbackDevice_WhenUserAgentIsNullOrEmpty(string? userAgent)
    {
        // Arrange
        var sender = new SesPasswordChangedEmailSender(_emailSenderMock);

        // Act
        await sender.SendAsync("neto@email.com", userAgent);

        // Assert
        await _emailSenderMock.Received(1).SendAsync(
            "neto@email.com",
            Arg.Any<string>(),
            Arg.Is<string>(html => html.Contains("Desconhecido")),
            Arg.Any<CancellationToken>());
    }
}
