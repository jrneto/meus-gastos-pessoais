using FluentAssertions;
using GastosApp.Application.Health.Queries.GetHealth;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetHealthQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnFallbackValues_WhenNoEnvironmentVariablesConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var handler = new GetHealthQueryHandler(configuration);

        // Act
        var result = await handler.Handle(new GetHealthQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("ok");
        result.Value.Version.Should().Be("local");
        result.Value.CommitSha.Should().Be("unknown");
        result.Value.Environment.Should().Be("local");
    }

    [Theory]
    [InlineData("hom")]
    [InlineData("prod")]
    public async Task Handle_ShouldReturnConfiguredValues_WhenEnvironmentVariablesPresent(string environment)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_VERSION"] = "v1.2.3",
                ["APP_COMMIT_SHA"] = "abc1234",
                ["APP_ENVIRONMENT"] = environment
            })
            .Build();
        var handler = new GetHealthQueryHandler(configuration);

        // Act
        var result = await handler.Handle(new GetHealthQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("v1.2.3");
        result.Value.CommitSha.Should().Be("abc1234");
        result.Value.Environment.Should().Be(environment);
    }
}
