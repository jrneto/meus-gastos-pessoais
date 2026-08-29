using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Reports.Queries.GetReports;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetReportsQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly GetReportsQueryHandler _handler;

    public GetReportsQueryHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _handler = new GetReportsQueryHandler(_transactionRepositoryMock, _categoryRepositoryMock);

        _categoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));
    }

    private static TransactionQueryItem Item(
        string id, long amountInCents, string categoryId = "category-1", DateOnly? date = null) =>
        new(id, "Descrição", amountInCents, categoryId, "despesa", date ?? new DateOnly(2026, 8, 15), "user-123", DateTimeOffset.UtcNow);

    private static Category BudgetedCategory(string id, string nome, long orcamentoMensalCents) =>
        Category.Restore(id, "account-123", nome, "despesa", orcamentoMensalCents, DateTimeOffset.UtcNow);

    // Configura a query do período atual (DateFrom=início do período) e a do período
    // anterior (DateFrom=início do período anterior) separadamente — o Handler faz duas
    // chamadas a QueryAsync com filtros diferentes.
    private void SetupCurrentPeriod(DateOnly start, TransactionQueryPage page) =>
        _transactionRepositoryMock
            .QueryAsync(Arg.Is<TransactionQueryFilter>(f => f.DateFrom == start), Arg.Any<CancellationToken>())
            .Returns(page);

    // ----- Filtro passado ao repositório -----

    [Fact]
    public async Task Handle_ShouldQueryTwice_WithTipoDespesaAndNoTruncationLimit()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");

        await _handler.Handle(query, CancellationToken.None);

        await _transactionRepositoryMock.Received(2).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f =>
                f.AccountId == "account-123"
                && f.Tipo == "despesa"
                && f.YearMonth == null
                && f.CategoryId == null
                && f.Cursor == null
                && f.Limit == int.MaxValue),
            Arg.Any<CancellationToken>());

        await _transactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.DateFrom == new DateOnly(2026, 8, 1) && f.DateTo == new DateOnly(2026, 8, 31)),
            Arg.Any<CancellationToken>());
        await _transactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f => f.DateFrom == new DateOnly(2026, 7, 1) && f.DateTo == new DateOnly(2026, 7, 31)),
            Arg.Any<CancellationToken>());
    }

    // ----- Total do período -----

    [Fact]
    public async Task Handle_ShouldSumTotalCents_FromCurrentPeriodTransactions()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage(
            [Item("t1", 30670), Item("t2", 94610)], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCents.Should().Be(125280);
        result.Value.Period.Should().Be("month");
        result.Value.StartDate.Should().Be(new DateOnly(2026, 8, 1));
        result.Value.EndDate.Should().Be(new DateOnly(2026, 8, 31));
    }

    // ----- Por categoria -----

    [Fact]
    public async Task Handle_ShouldIncludeOnlyCategoriesWithGastoGreaterThanZero_OrderedDescending()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage(
            [
                Item("t1", 30670, categoryId: "cat-menos-gasto"),
                Item("t2", 94610, categoryId: "cat-mais-gasto")
            ],
            null));
        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                BudgetedCategory("cat-menos-gasto", "Alimentacao", 80000),
                BudgetedCategory("cat-mais-gasto", "Transporte", 100000),
                Category.Restore("cat-sem-gasto", "account-123", "Lazer", "despesa", null, DateTimeOffset.UtcNow)
            });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.PorCategoria.Should().HaveCount(2);
        result.Value.PorCategoria.Select(c => c.CategoryId).Should().ContainInOrder("cat-mais-gasto", "cat-menos-gasto");
    }

    // ----- Maior gasto -----

    [Fact]
    public async Task Handle_ShouldReturnMaiorGasto_WithPercentualOrcamento_WhenCategoryHasOrcamentoDefined()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 43510, categoryId: "cat-1")], null));
        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-1", "Alimentacao", 80000) });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.MaiorGasto.Should().NotBeNull();
        result.Value.MaiorGasto!.CategoryId.Should().Be("cat-1");
        result.Value.MaiorGasto.GastoCents.Should().Be(43510);
        result.Value.MaiorGasto.PercentualOrcamento.Should().Be(54.4m);
    }

    [Fact]
    public async Task Handle_ShouldReturnMaiorGasto_WithNullPercentualOrcamento_WhenCategoryHasNoOrcamentoDefined()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 43510, categoryId: "cat-1")], null));
        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { Category.Restore("cat-1", "account-123", "Alimentacao", "despesa", null, DateTimeOffset.UtcNow) });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.MaiorGasto.Should().NotBeNull();
        result.Value.MaiorGasto!.PercentualOrcamento.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnNullMaiorGasto_WhenNoDespesaInPeriod()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.MaiorGasto.Should().BeNull();
        result.Value.PorCategoria.Should().BeEmpty();
    }

    // ----- Variação percentual -----

    [Fact]
    public async Task Handle_ShouldReturnPositiveVariacao_WhenCurrentTotalIsGreaterThanPrevious()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 138120)], null));
        _transactionRepositoryMock
            .QueryAsync(Arg.Is<TransactionQueryFilter>(f => f.DateFrom == new DateOnly(2026, 7, 1)), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item("t2", 123321)], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.VariacaoPercentual.Should().Be(12.0m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNegativeVariacao_WhenCurrentTotalIsLessThanPrevious()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 50000)], null));
        _transactionRepositoryMock
            .QueryAsync(Arg.Is<TransactionQueryFilter>(f => f.DateFrom == new DateOnly(2026, 7, 1)), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([Item("t2", 100000)], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.VariacaoPercentual.Should().Be(-50.0m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNullVariacao_WhenPreviousPeriodTotalIsZeroAndCurrentIsNot()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");
        SetupCurrentPeriod(new DateOnly(2026, 8, 1), new TransactionQueryPage([Item("t1", 50000)], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.VariacaoPercentual.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroVariacao_WhenBothPeriodsHaveZeroTotal()
    {
        var query = new GetReportsQuery("account-123", "month", "2026-08-15");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.TotalCents.Should().Be(0);
        result.Value.VariacaoPercentual.Should().Be(0m);
    }
}
