using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using GastosApp.Domain.Accounts;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetTransactionsQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly GetTransactionsQueryHandler _handler;

    public GetTransactionsQueryHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new GetTransactionsQueryHandler(_transactionRepositoryMock, _membershipRepositoryMock);
    }

    [Fact]
    public async Task Handle_ShouldMapFiltersAndReturnResult_WhenQueryIsValid()
    {
        // Arrange
        var query = new GetTransactionsQuery(
            "account-123", "user-123", "despesa", "2025-06", "category-1", "2025-06-01", "2025-06-30", 1000, 5000, null, 10);

        var page = new TransactionQueryPage(
            [
                new TransactionQueryItem("transaction-1", "Almoço", 4590, "category-1", "despesa",
                    new DateOnly(2025, 6, 15), "user-123", DateTimeOffset.UtcNow)
            ],
            "next-cursor-token");

        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(page);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be("transaction-1");
        result.Value.Items[0].CategoryId.Should().Be("category-1");
        result.Value.Items[0].CreatedByLabel.Should().Be("Você");
        result.Value.NextCursor.Should().Be("next-cursor-token");

        await _transactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f =>
                f.AccountId == "account-123"
                && f.Tipo == "despesa"
                && f.YearMonth == "2025-06"
                && f.CategoryId == "category-1"
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
        var query = new GetTransactionsQuery(
            "account-123", "user-123", null, null, null, null, null, null, null, null, null);

        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        await _transactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.Limit == 20 && f.CategoryId == null && f.Tipo == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyItems_WhenRepositoryReturnsNoResults()
    {
        // Arrange
        var query = new GetTransactionsQuery(
            "account-123", "user-123", null, null, null, null, null, null, null, null, null);

        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCacheCreatedByLabel_ForRepeatedAuthorInTheSamePage()
    {
        // Arrange
        var query = new GetTransactionsQuery(
            "account-123", "user-123", null, null, null, null, null, null, null, null, null);

        var page = new TransactionQueryPage(
            [
                new TransactionQueryItem("transaction-1", "Almoço", 4590, "category-1", "despesa",
                    new DateOnly(2025, 6, 15), "outro-user", DateTimeOffset.UtcNow),
                new TransactionQueryItem("transaction-2", "Uber", 3200, "category-2", "despesa",
                    new DateOnly(2025, 6, 14), "outro-user", DateTimeOffset.UtcNow)
            ],
            null);

        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(page);

        var membership = Membership.CreateTitular("account-123", "outro-user", "outro@example.com");
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "outro-user", Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Value.Items.Should().OnlyContain(item => item.CreatedByLabel == "outro@example.com");

        await _membershipRepositoryMock.Received(1).FindByAccountAndUserIdAsync(
            "account-123", "outro-user", Arg.Any<CancellationToken>());
    }
}
