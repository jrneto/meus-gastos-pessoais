using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Domain.Expenses;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateExpenseCommandHandlerTests
{
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly UpdateExpenseCommandHandler _handler;

    public UpdateExpenseCommandHandlerTests()
    {
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new UpdateExpenseCommandHandler(_expenseRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesExpense()
    {
        // Arrange
        var command = new UpdateExpenseCommand(
            "user-id-123", "expense-1", "Almoço atualizado", 5290, "category-1", new DateOnly(2025, 6, 16));

        var updatedExpense = Expense.Restore(
            "expense-1", "user-id-123", "Almoço atualizado", 5290,
            "category-1", new DateOnly(2025, 6, 16), DateTimeOffset.UtcNow);

        _expenseRepositoryMock.UpdateAsync(
            "user-id-123", "expense-1", "Almoço atualizado", 5290, "category-1",
            new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>())
            .Returns(updatedExpense);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("expense-1");
        result.Value.Description.Should().Be("Almoço atualizado");
        result.Value.AmountInCents.Should().Be(5290);
        result.Value.CategoryId.Should().Be("category-1");
        result.Value.ExpenseDate.Should().Be(new DateOnly(2025, 6, 16));

        await _expenseRepositoryMock.Received(1).UpdateAsync(
            "user-id-123", "expense-1", "Almoço atualizado", 5290, "category-1",
            new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRepositoryDoesNotFindExpense()
    {
        // Arrange
        var command = new UpdateExpenseCommand(
            "user-id-123", "expense-inexistente", "Almoço", 4590, "category-1", new DateOnly(2025, 6, 15));

        _expenseRepositoryMock.UpdateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(),
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }
}
