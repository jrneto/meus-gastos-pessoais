using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Commands.DeleteTransaction;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Transactions;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class DeleteTransactionCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly DeleteTransactionCommandHandler _handler;

    public DeleteTransactionCommandHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _handler = new DeleteTransactionCommandHandler(_transactionRepositoryMock);
    }

    private static Transaction SampleTransaction(string createdByUserId = "user-123") =>
        Transaction.Restore(
            "transaction-1", "account-123", "Almoço", 4590, "category-1", "despesa",
            new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryDeletesTransaction()
    {
        // Arrange
        var command = new DeleteTransactionCommand("account-123", "transaction-1", "user-123", MembershipRole.Total);
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction());
        _transactionRepositoryMock.DeleteAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _transactionRepositoryMock.Received(1).DeleteAsync(
            "account-123", "transaction-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        // Arrange
        var command = new DeleteTransactionCommand("account-123", "transaction-inexistente", "user-123", MembershipRole.Total);
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-inexistente", Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");

        await _transactionRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCallerIsLancarAndOwnsTheTransaction()
    {
        // Arrange
        var command = new DeleteTransactionCommand("account-123", "transaction-1", "user-123", MembershipRole.Lancar);
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-123"));
        _transactionRepositoryMock.DeleteAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsLancarAndDoesNotOwnTheTransaction()
    {
        // Arrange
        var command = new DeleteTransactionCommand("account-123", "transaction-1", "user-123", MembershipRole.Lancar);
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error!.Code.Should().Be("insufficient-permission");

        await _transactionRepositoryMock.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCallerIsTotalAndDoesNotOwnTheTransaction()
    {
        // Arrange — Total/Titular não são limitados por autoria
        var command = new DeleteTransactionCommand("account-123", "transaction-1", "user-123", MembershipRole.Total);
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));
        _transactionRepositoryMock.DeleteAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
