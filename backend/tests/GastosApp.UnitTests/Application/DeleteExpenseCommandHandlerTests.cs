using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Expenses.Commands.DeleteExpense;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class DeleteExpenseCommandHandlerTests
{
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly DeleteExpenseCommandHandler _handler;

    public DeleteExpenseCommandHandlerTests()
    {
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new DeleteExpenseCommandHandler(_expenseRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryDeletesExpense()
    {
        // Arrange
        var command = new DeleteExpenseCommand("user-id-123", "expense-1");
        _expenseRepositoryMock.DeleteAsync("user-id-123", "expense-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _expenseRepositoryMock.Received(1).DeleteAsync(
            "user-id-123", "expense-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRepositoryDoesNotDeleteExpense()
    {
        // Arrange
        var command = new DeleteExpenseCommand("user-id-123", "expense-inexistente");
        _expenseRepositoryMock.DeleteAsync("user-id-123", "expense-inexistente", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }
}
