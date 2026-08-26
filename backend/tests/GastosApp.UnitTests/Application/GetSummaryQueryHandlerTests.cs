using FluentAssertions;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.Summary.Queries.GetSummary;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Categories;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class GetSummaryQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepositoryMock;
    private readonly ICategoryRepository _categoryRepositoryMock;
    private readonly IMembershipRepository _membershipRepositoryMock;
    private readonly GetSummaryQueryHandler _handler;

    public GetSummaryQueryHandlerTests()
    {
        _transactionRepositoryMock = Substitute.For<ITransactionRepository>();
        _categoryRepositoryMock = Substitute.For<ICategoryRepository>();
        _membershipRepositoryMock = Substitute.For<IMembershipRepository>();
        _handler = new GetSummaryQueryHandler(_transactionRepositoryMock, _categoryRepositoryMock, _membershipRepositoryMock);

        _categoryRepositoryMock.ListAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<Category>());
    }

    private static TransactionQueryItem Item(
        string id, long amountInCents, string tipo, string categoryId = "category-1",
        string createdByUserId = "user-123", DateOnly? date = null) =>
        new(id, "Descrição", amountInCents, categoryId, tipo, date ?? new DateOnly(2026, 8, 15), createdByUserId, DateTimeOffset.UtcNow);

    private static Category BudgetedCategory(string id, string nome, long orcamentoMensalCents) =>
        Category.Restore(id, "account-123", nome, "despesa", orcamentoMensalCents, DateTimeOffset.UtcNow);

    // ----- Filtro passado ao repositório -----

    [Fact]
    public async Task Handle_ShouldQueryWithMonthAsYearMonth_AndNoTruncationLimit()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        await _handler.Handle(query, CancellationToken.None);

        await _transactionRepositoryMock.Received(1).QueryAsync(
            Arg.Is<TransactionQueryFilter>(f =>
                f.AccountId == "account-123"
                && f.YearMonth == "2026-08"
                && f.Tipo == null
                && f.CategoryId == null
                && f.Cursor == null
                && f.Limit == int.MaxValue),
            Arg.Any<CancellationToken>());
    }

    // ----- Soma de receitas/gasto/saldo -----

    [Fact]
    public async Task Handle_ShouldSumReceitasAndGastoAndComputeSaldo_FromMixedTransactions()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        var page = new TransactionQueryPage(
            [
                Item("t1", 520000, "receita"),
                Item("t2", 30670, "despesa"),
                Item("t3", 94610, "despesa")
            ],
            null);
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>()).Returns(page);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReceitasCents.Should().Be(520000);
        result.Value.GastoCents.Should().Be(125280);
        result.Value.SaldoCents.Should().Be(394720);
        result.Value.Month.Should().Be("2026-08");
    }

    // ----- Orçamento total / por categoria -----

    [Fact]
    public async Task Handle_ShouldSumOrcamentoTotal_OnlyFromDespesaCategoriesWithOrcamentoDefined()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        // ICategoryRepository.ListAsync já é chamado com tipo="despesa" — o
        // mock só devolve categorias de despesa com orçamento definido, mesmo
        // comportamento do repositório real filtrando por tipo.
        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                BudgetedCategory("cat-1", "Alimentacao", 80000),
                BudgetedCategory("cat-2", "Transporte", 30000)
            });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.OrcamentoTotalCents.Should().Be(110000);
    }

    [Fact]
    public async Task Handle_ShouldExcludeDespesaCategoryWithoutOrcamento_FromPorCategoriaAndOrcamentoTotal()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                Category.Restore("cat-1", "account-123", "SemOrcamento", "despesa", null, DateTimeOffset.UtcNow)
            });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.OrcamentoTotalCents.Should().Be(0);
        result.Value.PorCategoria.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeCategoryWithZeroGasto_InPorCategoria()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-1", "Alimentacao", 80000) });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.PorCategoria.Should().ContainSingle();
        result.Value.PorCategoria[0].CategoryId.Should().Be("cat-1");
        result.Value.PorCategoria[0].GastoCents.Should().Be(0);
        result.Value.PorCategoria[0].OrcamentoMensalCents.Should().Be(80000);
    }

    [Fact]
    public async Task Handle_ShouldOrderPorCategoriaByGastoDescending()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        var page = new TransactionQueryPage(
            [
                Item("t1", 30670, "despesa", categoryId: "cat-1"),
                Item("t2", 94610, "despesa", categoryId: "cat-2")
            ],
            null);
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>()).Returns(page);

        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category>
            {
                BudgetedCategory("cat-1", "Alimentacao", 80000),
                BudgetedCategory("cat-2", "Transporte", 100000)
            });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.PorCategoria.Select(c => c.CategoryId).Should().ContainInOrder("cat-2", "cat-1");
    }

    // ----- Restante -----

    [Fact]
    public async Task Handle_ShouldReturnNegativeRestante_WhenGastoExceedsOrcamentoTotal()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        var page = new TransactionQueryPage([Item("t1", 200000, "despesa")], null);
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>()).Returns(page);

        _categoryRepositoryMock.ListAsync("account-123", "despesa", Arg.Any<CancellationToken>())
            .Returns(new List<Category> { BudgetedCategory("cat-1", "Alimentacao", 80000) });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.RestanteCents.Should().Be(-120000);
    }

    // ----- Mês vazio -----

    [Fact]
    public async Task Handle_ShouldReturnAllZeros_WhenNoTransactionsInMonth()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-01");
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage([], null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.SaldoCents.Should().Be(0);
        result.Value.ReceitasCents.Should().Be(0);
        result.Value.GastoCents.Should().Be(0);
        result.Value.UltimosLancamentos.Should().BeEmpty();
    }

    // ----- Últimos lançamentos -----

    [Fact]
    public async Task Handle_ShouldLimitUltimosLancamentosToFive_WhenMoreThanFiveTransactionsInMonth()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        var items = Enumerable.Range(1, 7)
            .Select(i => Item($"t{i}", 1000 * i, "despesa"))
            .ToList();
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryPage(items, null));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.UltimosLancamentos.Should().HaveCount(5);
        result.Value.UltimosLancamentos.Select(t => t.Id).Should().ContainInOrder("t1", "t2", "t3", "t4", "t5");
    }

    [Fact]
    public async Task Handle_ShouldCacheCreatedByLabel_ForRepeatedAuthorAmongRecentTransactions()
    {
        var query = new GetSummaryQuery("account-123", "user-123", "2026-08");
        var page = new TransactionQueryPage(
            [
                Item("t1", 1000, "despesa", createdByUserId: "outro-user"),
                Item("t2", 2000, "despesa", createdByUserId: "outro-user")
            ],
            null);
        _transactionRepositoryMock.QueryAsync(Arg.Any<TransactionQueryFilter>(), Arg.Any<CancellationToken>()).Returns(page);

        var membership = Membership.CreateTitular("account-123", "outro-user", "outro@example.com");
        _membershipRepositoryMock.FindByAccountAndUserIdAsync("account-123", "outro-user", Arg.Any<CancellationToken>())
            .Returns(membership);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.UltimosLancamentos.Should().OnlyContain(t => t.CreatedByLabel == "outro@example.com");
        await _membershipRepositoryMock.Received(1).FindByAccountAndUserIdAsync(
            "account-123", "outro-user", Arg.Any<CancellationToken>());
    }
}
