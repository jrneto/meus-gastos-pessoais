using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Domain.Transactions;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class RegisterTransactionCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly RegisterTransactionCommandHandler _handler;

    public RegisterTransactionCommandHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _handler = new RegisterTransactionCommandHandler(_transactionRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldRegisterTransactionSuccessfully_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterTransactionCommand(
            "account-123", "Almoço no restaurante", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(command.Description);
        result.Value.AmountInCents.Should().Be(command.AmountInCents);
        result.Value.CategoryId.Should().Be("category-1");
        result.Value.Tipo.Should().Be("despesa");
        result.Value.Date.Should().Be(command.Date);
        result.Value.Id.Should().NotBeNullOrWhiteSpace();
        result.Value.CreatedByUserId.Should().Be("user-123");
        result.Value.CreatedByLabel.Should().Be("Você");

        await _transactionRepositoryMock.Received(1).SaveAsync(
            Arg.Is<Transaction>(t => t.AccountId == command.AccountId && t.Description == command.Description),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRegisterReceita_WhenTipoIsReceita()
    {
        var command = new RegisterTransactionCommand(
            "account-123", "Salário", 500000, "category-2", "receita", new DateOnly(2025, 6, 15), "user-123");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be("receita");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Handle_ShouldAcceptRetroactiveAndFutureDates(int daysOffset)
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysOffset));
        var command = new RegisterTransactionCommand("account-123", "Transação", 100, "category-1", "despesa", date, "user-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Date.Should().Be(date);
    }
}
