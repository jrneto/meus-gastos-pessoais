using FluentAssertions;
using GastosApp.Domain.Accounts;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class MembershipTests
{
    [Fact]
    public void CreateTitular_ShouldSetRoleTitularStatusAtivoAndCreatedAt()
    {
        // Act
        var membership = Membership.CreateTitular("account-123", "user-456", "titular@email.com");

        // Assert
        membership.Id.Should().NotBeNullOrWhiteSpace();
        membership.AccountId.Should().Be("account-123");
        membership.UserId.Should().Be("user-456");
        membership.Email.Should().Be("titular@email.com");
        membership.Role.Should().Be(MembershipRole.Titular);
        membership.Status.Should().Be(MembershipStatus.Ativo);
        membership.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateTitular_ShouldGenerateDifferentIds_ForDifferentCalls()
    {
        var first = Membership.CreateTitular("account-1", "user-1", "user1@email.com");
        var second = Membership.CreateTitular("account-1", "user-2", "user2@email.com");

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void CreateInvite_ShouldSetStatusConvitePendenteAndNoUserId()
    {
        // Act
        var membership = Membership.CreateInvite("account-123", "convidado@email.com", MembershipRole.Leitura);

        // Assert
        membership.Id.Should().NotBeNullOrWhiteSpace();
        membership.AccountId.Should().Be("account-123");
        membership.UserId.Should().BeNull();
        membership.Email.Should().Be("convidado@email.com");
        membership.Role.Should().Be(MembershipRole.Leitura);
        membership.Status.Should().Be(MembershipStatus.ConvitePendente);
        membership.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Restore_ShouldKeepGivenData()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var membership = Membership.Restore(
            "membership-1", "account-123", "user-456", "titular@email.com",
            MembershipRole.Titular, MembershipStatus.Ativo, createdAt);

        // Assert
        membership.Id.Should().Be("membership-1");
        membership.AccountId.Should().Be("account-123");
        membership.UserId.Should().Be("user-456");
        membership.Email.Should().Be("titular@email.com");
        membership.Role.Should().Be(MembershipRole.Titular);
        membership.Status.Should().Be(MembershipStatus.Ativo);
        membership.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Restore_ShouldAllowNullUserId_ForPendingInvite()
    {
        var createdAt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var membership = Membership.Restore(
            "membership-1", "account-123", null, "convidado@email.com",
            MembershipRole.Total, MembershipStatus.ConvitePendente, createdAt);

        membership.UserId.Should().BeNull();
        membership.Status.Should().Be(MembershipStatus.ConvitePendente);
    }
}
