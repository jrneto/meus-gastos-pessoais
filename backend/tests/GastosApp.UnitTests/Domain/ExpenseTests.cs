using FluentAssertions;
using GastosApp.Domain.Expenses;
using Xunit;

namespace GastosApp.UnitTests.Domain;

public class ExpenseTests
{
    [Fact]
    public void Create_ShouldGenerateIdAndCreatedAt_FromGivenData()
    {
        // Arrange
        var expenseDate = new DateOnly(2025, 6, 15);

        // Act
        var expense = Expense.Create("user-id-123", "Almoço no restaurante", 4590, ExpenseCategory.Alimentacao, expenseDate);

        // Assert
        expense.Id.Should().NotBeNullOrWhiteSpace();
        expense.UserId.Should().Be("user-id-123");
        expense.Description.Should().Be("Almoço no restaurante");
        expense.AmountInCents.Should().Be(4590);
        expense.Category.Should().Be(ExpenseCategory.Alimentacao);
        expense.ExpenseDate.Should().Be(expenseDate);
        expense.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateDifferentIds_ForDifferentExpenses()
    {
        // Act
        var first = Expense.Create("user-id-123", "Despesa 1", 100, ExpenseCategory.Outros, new DateOnly(2025, 6, 15));
        var second = Expense.Create("user-id-123", "Despesa 2", 200, ExpenseCategory.Outros, new DateOnly(2025, 6, 15));

        // Assert
        first.Id.Should().NotBe(second.Id);
    }
}