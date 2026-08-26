using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Transactions;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class UpdateTransactionCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly UpdateTransactionCommandHandler _handler;

    public UpdateTransactionCommandHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new UpdateTransactionCommandHandler(_transactionRepositoryMock, _membershipRepositoryMock);
    }

    private static Transaction SampleTransaction(string createdByUserId = "user-123") =>
        Transaction.Restore(
            "transaction-1", "account-123", "Almoço", 4590, "category-1", "despesa",
            new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRepositoryUpdatesTransaction()
    {
        // Arrange
        var command = new UpdateTransactionCommand(
            "account-123", "transaction-1", "user-123", MembershipRole.Total,
            "Almoço atualizado", 5290, "category-1", "despesa", new DateOnly(2025, 6, 16));

        var updated = Transaction.Restore(
            "transaction-1", "account-123", "Almoço atualizado", 5290,
            "category-1", "despesa", new DateOnly(2025, 6, 16), "user-123", DateTimeOffset.UtcNow);

        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction());
        _transactionRepositoryMock.UpdateAsync(
            "account-123", "transaction-1", "Almoço atualizado", 5290, "category-1", "despesa",
            new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>())
            .Returns(updated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("transaction-1");
        result.Value.Description.Should().Be("Almoço atualizado");
        result.Value.AmountInCents.Should().Be(5290);
        result.Value.CategoryId.Should().Be("category-1");
        result.Value.Date.Should().Be(new DateOnly(2025, 6, 16));
        result.Value.CreatedByLabel.Should().Be("Você");

        await _transactionRepositoryMock.Received(1).UpdateAsync(
            "account-123", "transaction-1", "Almoço atualizado", 5290, "category-1", "despesa",
            new DateOnly(2025, 6, 16), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        // Arrange
        var command = new UpdateTransactionCommand(
            "account-123", "transaction-inexistente", "user-123", MembershipRole.Total,
            "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-inexistente", Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");

        await _transactionRepositoryMock.DidNotReceiveWithAnyArgs().UpdateAsync(
            default!, default!, default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCallerIsLancarAndOwnsTheTransaction()
    {
        // Arrange
        var command = new UpdateTransactionCommand(
            "account-123", "transaction-1", "user-123", MembershipRole.Lancar,
            "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-123"));
        _transactionRepositoryMock.UpdateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-123"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsLancarAndDoesNotOwnTheTransaction()
    {
        // Arrange
        var command = new UpdateTransactionCommand(
            "account-123", "transaction-1", "user-123", MembershipRole.Lancar,
            "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error!.Code.Should().Be("insufficient-permission");

        await _transactionRepositoryMock.DidNotReceiveWithAnyArgs().UpdateAsync(
            default!, default!, default!, default, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenCallerIsTotalAndDoesNotOwnTheTransaction()
    {
        // Arrange — Total/Titular não são limitados por autoria
        var command = new UpdateTransactionCommand(
            "account-123", "transaction-1", "user-123", MembershipRole.Total,
            "Almoço", 4590, "category-1", "despesa", new DateOnly(2025, 6, 15));

        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));
        _transactionRepositoryMock.UpdateAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
