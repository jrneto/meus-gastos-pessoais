using FluentAssertions;
using GastosApp.Domain.Transactions;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class TransactionTests
{
    [Fact]
    public void Create_ShouldGenerateIdAndCreatedAt_FromGivenData()
    {
        // Arrange
        var date = new DateOnly(2025, 6, 15);

        // Act
        var transaction = Transaction.Create("account-id-123", "Almoço no restaurante", 4590, "category-1", "despesa", date, "user-id-123");

        // Assert
        transaction.Id.Should().NotBeNullOrWhiteSpace();
        transaction.AccountId.Should().Be("account-id-123");
        transaction.Description.Should().Be("Almoço no restaurante");
        transaction.AmountInCents.Should().Be(4590);
        transaction.CategoryId.Should().Be("category-1");
        transaction.Tipo.Should().Be("despesa");
        transaction.Date.Should().Be(date);
        transaction.CreatedByUserId.Should().Be("user-id-123");
        transaction.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateDifferentIds_ForDifferentTransactions()
    {
        // Act
        var first = Transaction.Create("account-id-123", "Transação 1", 100, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-id-123");
        var second = Transaction.Create("account-id-123", "Transação 2", 200, "category-1", "receita", new DateOnly(2025, 6, 15), "user-id-123");

        // Assert
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Restore_ShouldPreserveAllFields_IncludingCreatedByUserId()
    {
        // Arrange
        var date = new DateOnly(2025, 6, 15);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);

        // Act
        var transaction = Transaction.Restore(
            "transaction-1", "account-id-123", "Salário", 500000, "category-2", "receita", date, "user-id-456", createdAt);

        // Assert
        transaction.Id.Should().Be("transaction-1");
        transaction.AccountId.Should().Be("account-id-123");
        transaction.Tipo.Should().Be("receita");
        transaction.CreatedByUserId.Should().Be("user-id-456");
        transaction.CreatedAt.Should().Be(createdAt);
    }
}
