using System.Text;
using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Transactions.Queries.ExportTransactions;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class ExportTransactionsQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly ExportTransactionsQueryHandler _handler;

    public ExportTransactionsQueryHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new ExportTransactionsQueryHandler(_transactionRepositoryMock, _categoryRepositoryMock, _membershipRepositoryMock);

        _categoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));
    }

    private static ExportTransactionsQuery EmptyQuery(string accountId = "account-123", string callerUserId = "user-123") =>
        new(accountId, callerUserId, null, null, null, null, null, null, null);

    private static TransactionQueryItem Item(
        string id = "transaction-1", string categoryId = "category-1", string createdByUserId = "user-123", string tipo = "despesa") =>
        new(id, "Almoço", 4590, categoryId, tipo, new DateOnly(2025, 6, 15), createdByUserId, DateTimeOffset.UtcNow);

    private static string DecodeCsv(byte[] bytes) => Encoding.UTF8.GetString(bytes.Skip(3).ToArray());

    [Fact]
    public async Task Handle_ShouldQueryRepository_WithCursorNullAndNoTruncationLimit()
    {
        var query = new ExportTransactionsQuery(
            "account-123", "user-123", "despesa", "2025-06", "category-1", "2025-06-01", "2025-06-30", 1000, 5000);

        await _handler.Handle(query, CancellationToken.None);

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
                && f.Cursor == null
                && f.Limit == int.MaxValue),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldListCategories_WithNoTipoFilter()
    {
        var query = EmptyQuery();

        await _handler.Handle(query, CancellationToken.None);

        await _categoryRepositoryMock.Received(1).ListAsync("account-123", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldResolveCategoryName_FromCategoryId()
    {
        var query = EmptyQuery();
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(categoryId: "category-1")], null));
        _categoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category> { Category.Restore("category-1", "account-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow) });

        var result = await _handler.Handle(query, CancellationToken.None);

        DecodeCsv(result.Value).Should().Contain(";Alimentacao;");
    }

    [Fact]
    public async Task Handle_ShouldFallbackToCategoryId_WhenCategoryNoLongerExists()
    {
        var query = EmptyQuery();
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(categoryId: "categoria-excluida")], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        DecodeCsv(result.Value).Should().Contain(";categoria-excluida;");
    }

    [Fact]
    public async Task Handle_ShouldReturnLancadoPorVoce_WhenCreatedByUserIdIsTheCaller()
    {
        var query = EmptyQuery(callerUserId: "user-123");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(createdByUserId: "user-123")], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        DecodeCsv(result.Value).Should().Contain(";Você\r\n");
    }

    [Fact]
    public async Task Handle_ShouldReturnMembroEmail_WhenCreatedByAnotherActiveMember()
    {
        var query = EmptyQuery(accountId: "account-123", callerUserId: "user-123");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(createdByUserId: "outro-membro")], null));
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "outro-membro", Arg.Any<CancellationToken>())
            .Returns(Membership.CreateTitular("account-123", "outro-membro", "outro@membro.com"));

        var result = await _handler.Handle(query, CancellationToken.None);

        DecodeCsv(result.Value).Should().Contain(";outro@membro.com\r\n");
    }

    [Fact]
    public async Task Handle_ShouldReturnExMembro_WhenMembershipNoLongerExists()
    {
        var query = EmptyQuery();
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item(createdByUserId: "membro-removido")], null));
        _membershipRepositoryMock.FindByAccountAndUserIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Membership?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        DecodeCsv(result.Value).Should().Contain(";Ex-membro\r\n");
    }

    [Fact]
    public async Task Handle_ShouldCacheCreatedByLabel_ForRepeatedAuthorInTheSamePage()
    {
        var query = EmptyQuery();
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage(
            [
                Item(id: "transaction-1", createdByUserId: "outro-membro"),
                Item(id: "transaction-2", createdByUserId: "outro-membro")
            ],
            null));
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "outro-membro", Arg.Any<CancellationToken>())
            .Returns(Membership.CreateTitular("account-123", "outro-membro", "outro@membro.com"));

        await _handler.Handle(query, CancellationToken.None);

        await _membershipRepositoryMock.Received(1).FindByAccountAndUserIdAsync(
            "account-123", "outro-membro", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnCsvWithOnlyHeader_WhenRepositoryReturnsNoItems()
    {
        var query = EmptyQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        DecodeCsv(result.Value).Should().Be("data;descricao;categoria;tipo;valor;lancadoPor\r\n");
    }
}
