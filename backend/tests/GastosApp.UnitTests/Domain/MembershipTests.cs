using FluentAssertions;
using GastosApp.Domain.Accounts;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class MembershipTests
{
    [Fact]
    public void CreateTitular_ShouldSetRoleTitularAndCreatedAt()
    {
        // Act
        var membership = Membership.CreateTitular("account-123", "user-456");

        // Assert
        membership.AccountId.Should().Be("account-123");
        membership.UserId.Should().Be("user-456");
        membership.Role.Should().Be(MembershipRole.Titular);
        membership.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Restore_ShouldKeepGivenData()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var membership = Membership.Restore("account-123", "user-456", MembershipRole.Titular, createdAt);

        // Assert
        membership.AccountId.Should().Be("account-123");
        membership.UserId.Should().Be("user-456");
        membership.Role.Should().Be(MembershipRole.Titular);
        membership.CreatedAt.Should().Be(createdAt);
    }
}
