using FluentAssertions;
using GastosApp.Domain.Accounts;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class AccountTests
{
    [Fact]
    public void Create_ShouldGenerateIdAndCreatedAt()
    {
        // Act
        var account = Account.Create();

        // Assert
        account.Id.Should().NotBeNullOrWhiteSpace();
        account.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateDifferentIds_ForDifferentAccounts()
    {
        // Act
        var first = Account.Create();
        var second = Account.Create();

        // Assert
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Restore_ShouldKeepGivenIdAndCreatedAt()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var account = Account.Restore("account-123", createdAt);

        // Assert
        account.Id.Should().Be("account-123");
        account.CreatedAt.Should().Be(createdAt);
    }
}
