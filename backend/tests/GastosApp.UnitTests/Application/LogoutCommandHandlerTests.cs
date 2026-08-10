using FluentAssertions;
using GastosApp.Application.Auth.Commands.Logout;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class LogoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldAlwaysReturnSuccess()
    {
        // Arrange
        var handler = new LogoutCommandHandler();

        // Act
        var result = await handler.Handle(new LogoutCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
