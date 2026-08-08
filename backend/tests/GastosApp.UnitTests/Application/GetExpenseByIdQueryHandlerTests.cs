using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Expenses.Queries.GetExpenseById;
using GastosApp.Domain.Expenses;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetExpenseByIdQueryHandlerTests
{
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly GetExpenseByIdQueryHandler _handler;

    public GetExpenseByIdQueryHandlerTests()
    {
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new GetExpenseByIdQueryHandler(_expenseRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryFindsExpense()
    {
        // Arrange
        var query = new GetExpenseByIdQuery("user-id-123", "expense-1");

        var expense = Expense.Restore(
            "expense-1", "user-id-123", "Almoço no restaurante", 4590,
            ExpenseCategory.Alimentacao, new DateOnly(2025, 6, 15), DateTimeOffset.UtcNow);

        _expenseRepositoryMock.GetByIdAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(expense);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("expense-1");
        result.Value.Description.Should().Be("Almoço no restaurante");
        result.Value.AmountInCents.Should().Be(4590);
        result.Value.Category.Should().Be("Alimentacao");
        result.Value.ExpenseDate.Should().Be(new DateOnly(2025, 6, 15));
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRepositoryDoesNotFindExpense()
    {
        // Arrange
        var query = new GetExpenseByIdQuery("user-id-123", "expense-inexistente");

        _expenseRepositoryMock.GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }
}
