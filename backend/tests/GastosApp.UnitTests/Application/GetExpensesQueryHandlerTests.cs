using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Expenses.Queries.GetExpenses;
using GastosApp.Domain.Expenses;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetExpensesQueryHandlerTests
{
    private readonly IExpenseRepository _expenseRepositoryMock;
    private readonly GetExpensesQueryHandler _handler;

    public GetExpensesQueryHandlerTests()
    {
        _expenseRepositoryMock = Substitute.For<IExpenseRepository>();
        _handler = new GetExpensesQueryHandler(_expenseRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldMapFiltersAndReturnResult_WhenQueryIsValid()
    {
        // Arrange
        var query = new GetExpensesQuery(
            "user-id-123", "2025-06", "Alimentacao", "2025-06-01", "2025-06-30", 1000, 5000, null, 10);

        var page = new ExpenseQueryPage(
            [
                new ExpenseQueryItem("expense-1", "Almoço", 4590, ExpenseCategory.Alimentacao,
                    new DateOnly(2025, 6, 15), DateTimeOffset.UtcNow)
            ],
            "next-cursor-token");

        _expenseRepositoryMock.QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(page);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be("expense-1");
        result.Value.Items[0].Category.Should().Be("Alimentacao");
        result.Value.NextCursor.Should().Be("next-cursor-token");

        await _expenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f =>
                f.UserId == "user-id-123"
                && f.YearMonth == "2025-06"
                && f.Category == ExpenseCategory.Alimentacao
                && f.DateFrom == new DateOnly(2025, 6, 1)
                && f.DateTo == new DateOnly(2025, 6, 30)
                && f.MinAmountInCents == 1000
                && f.MaxAmountInCents == 5000
                && f.Limit == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldApplyDefaultLimit_WhenLimitIsNotInformed()
    {
        // Arrange
        var query = new GetExpensesQuery(
            "user-id-123", null, null, null, null, null, null, null, null);

        _expenseRepositoryMock.QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new ExpenseQueryPage([], null));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        await _expenseRepositoryMock.Received(1).QueryAsync(
            Arg.Is<ExpenseQueryFilter>(f => f.Limit == 20 && f.Category == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyItems_WhenRepositoryReturnsNoResults()
    {
        // Arrange
        var query = new GetExpensesQuery(
            "user-id-123", null, null, null, null, null, null, null, null);

        _expenseRepositoryMock.QueryAsync(Arg.Any<ExpenseQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new ExpenseQueryPage([], null));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }
}
