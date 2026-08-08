using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Domain.Expenses;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterExpenseCommandHandlerTests
{
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly RegisterExpenseCommandHandler _handler;

    public RegisterExpenseCommandHandlerTests()
    {
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new RegisterExpenseCommandHandler(_expenseRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldRegisterExpenseSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterExpenseCommand(
            "user-id-123", "Almoço no restaurante", 4590, "Alimentacao", new DateOnly(2025, 6, 15));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(command.Description);
        result.Value.AmountInCents.Should().Be(command.AmountInCents);
        result.Value.Category.Should().Be("Alimentacao");
        result.Value.ExpenseDate.Should().Be(command.ExpenseDate);
        result.Value.Id.Should().NotBeNullOrWhiteSpace();

        await _expenseRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Expense>(e => e.UserId == command.UserId && e.Description == command.Description),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Handle_ShouldAcceptRetroactiveAndFutureDates(int daysOffset)
    {
        // Arrange
        var expenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysOffset));
        var command = new RegisterExpenseCommand("user-id-123", "Despesa", 100, "Outros", expenseDate);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpenseDate.Should().Be(expenseDate);
    }
}