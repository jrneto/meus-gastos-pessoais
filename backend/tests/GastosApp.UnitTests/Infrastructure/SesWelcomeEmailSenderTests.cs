using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Users;
using GastosApp.Infrastructure.Email;
using NSubstitute;

namespace GastosApp.UnitTests.Infrastructure;

public class SesWelcomeEmailSenderTests
{
    private readonly IEmailSender _emailSenderMock;
    private readonly IUserProfileRepository _profileRepositoryMock;
    private readonly SesWelcomeEmailSender _sender;

    public SesWelcomeEmailSenderTests()
    {
        _emailSenderMock = Substitute.For<IEmailSender>();
        _profileRepositoryMock = Substitute.For<IUserProfileRepository>();
        _sender = new SesWelcomeEmailSender(_emailSenderMock, _profileRepositoryMock);
    }

    [Fact]
    public async Task SendAsync_ShouldCallEmailSender_WithSubjectAndFilledTemplate()
    {
        // Arrange
        var profile = UserProfile.Create("user-1", "Neto", "11999998888", "12345678909");
        _profileRepositoryMock.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(profile);

        // Act
        await _sender.SendAsync("user-1", "neto@email.com");

        // Assert
        await _emailSenderMock.Received(1).SendAsync(
            "neto@email.com",
            "Bem-vindo ao jrn.expenses",
            Arg.Is<string>(html =>
                html.Contains("Bem-vindo, Neto.") &&
                html.Contains("neto@email.com") &&
                !html.Contains("{{nome}}") &&
                !html.Contains("{{email}}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ShouldThrow_WhenProfileNotFound()
    {
        // Arrange — sem fallback textual (decisão do usuário, ver spec.md
        // decisão 2): perfil ausente é tratado como falha, não como um
        // caminho de conteúdo alternativo.
        _profileRepositoryMock.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((UserProfile?)null);

        // Act
        var act = async () => await _sender.SendAsync("user-1", "neto@email.com");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _emailSenderMock.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default);
    }
}
