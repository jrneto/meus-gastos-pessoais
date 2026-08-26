using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Queries.GetTransactionById;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Transactions;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetTransactionByIdQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly GetTransactionByIdQueryHandler _handler;

    public GetTransactionByIdQueryHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new GetTransactionByIdQueryHandler(_transactionRepositoryMock, _membershipRepositoryMock);
    }

    private static Transaction SampleTransaction(string createdByUserId) =>
        Transaction.Restore(
            "transaction-1", "account-123", "Almoço no restaurante", 4590, "category-1", "despesa",
            new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_ShouldReturnCreatedByLabelVoce_WhenCallerIsTheAuthor()
    {
        // Arrange
        var query = new GetTransactionByIdQuery("account-123", "transaction-1", "user-123");
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "user-123"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("transaction-1");
        result.Value.CreatedByLabel.Should().Be("Você");

        await _membershipRepositoryMock.DidNotReceiveWithAnyArgs().FindByAccountAndUserIdAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldReturnAuthorEmail_WhenCallerIsAnotherMember()
    {
        // Arrange
        var query = new GetTransactionByIdQuery("account-123", "transaction-1", "user-123");
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "outro-user"));

        var membership = Membership.CreateTitular("account-123", "outro-user", "outro@example.com");
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "outro-user", Arg.Any<CancellationToken>())
            .Returns(membership);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedByLabel.Should().Be("outro@example.com");
    }

    [Fact]
    public async Task Handle_ShouldReturnExMembro_WhenAuthorMembershipNoLongerExists()
    {
        // Arrange
        var query = new GetTransactionByIdQuery("account-123", "transaction-1", "user-123");
        _transactionRepositoryMock.GetByIdAsync("account-123", "transaction-1", Arg.Any<CancellationToken>())
            .Returns(SampleTransaction(createdByUserId: "membro-removido"));
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "membro-removido", Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedByLabel.Should().Be("Ex-membro");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRepositoryDoesNotFindTransaction()
    {
        // Arrange
        var query = new GetTransactionByIdQuery("account-123", "transaction-inexistente", "user-123");

        _transactionRepositoryMock.GetByIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error!.Code.Should().Be("not-found");
    }
}
