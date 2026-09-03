using FluentAssertions;
using GastosApp.Application.Accounts.Commands.EnsureAccount;
using GastosApp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class EnsureAccountCommandHandlerTests
{
    private readonly IAccountRepository _accountRepositoryMock;
    private readonly IWelcomeEmailSender _welcomeEmailSenderMock;
    private readonly IServiceProvider _serviceProviderMock;
    private readonly ILogger<EnsureAccountCommandHandler> _loggerMock;
    private readonly EnsureAccountCommandHandler _handler;

    public EnsureAccountCommandHandlerTests()
    {
        _accountRepositoryMock = Substitute.For<IAccountRepository>();
        _welcomeEmailSenderMock = Substitute.For<IWelcomeEmailSender>();
        _loggerMock = Substitute.For<ILogger<EnsureAccountCommandHandler>>();

        // IWelcomeEmailSender é resolvido tardiamente via IServiceProvider
        // (não injeção direta) — ver comentário no construtor de
        // EnsureAccountCommandHandler. Mock retorna o sender de cima quando
        // GetRequiredService<IWelcomeEmailSender>() é chamado.
        _serviceProviderMock = Substitute.For<IServiceProvider>();
        _serviceProviderMock.GetService(typeof(IWelcomeEmailSender)).Returns(_welcomeEmailSenderMock);

        _handler = new EnsureAccountCommandHandler(_accountRepositoryMock, _serviceProviderMock, _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingAccount_WhenAlreadyResolvable()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns("account-existente");

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-existente");
        result.Value.AlreadyExisted.Should().BeTrue();

        await _accountRepositoryMock.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldCreateAccountWithEmail_WhenNoneExistsYet()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-novo");
        result.Value.AlreadyExisted.Should().BeFalse();

        await _accountRepositoryMock.Received(1).CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnWinnerAccount_WhenCreateResolvesConcurrentConflict()
    {
        // Arrange — corrida: FindAccountIdByUserIdAsync não achou, mas
        // CreateAsync recuperou o vencedor de uma criação concorrente
        // (ex.: trigger do Cognito criou entre o Find e o Create).
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-do-vencedor", AlreadyExisted: true));

        // Act
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-do-vencedor");
        result.Value.AlreadyExisted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldSendWelcomeEmail_WhenAccountIsCreatedForTheFirstTime()
    {
        // Arrange
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));

        // Act
        await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        await _welcomeEmailSenderMock.Received(1).SendAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)] // FindAccountIdByUserIdAsync resolve direto, sem passar por CreateAsync
    [InlineData(false)] // CreateAsync resolve a corrida (AlreadyExisted: true)
    public async Task Handle_ShouldNotSendWelcomeEmail_WhenAccountAlreadyExisted(bool resolvedByFind)
    {
        // Arrange
        if (resolvedByFind)
        {
            _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
                .Returns("account-existente");
        }
        else
        {
            _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
                .Returns((string?)null);
            _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
                .Returns(new CreateAccountResult("account-do-vencedor", AlreadyExisted: true));
        }

        // Act
        await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        await _welcomeEmailSenderMock.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ShouldNotPropagate_WhenWelcomeEmailSenderThrows()
    {
        // Arrange — falha no envio do email de boas-vindas nunca pode
        // derrubar EnsureAccountCommand: a conta já foi criada de fato
        // (ver spec.md/plan.md, mesma filosofia defensiva do
        // ResetPasswordCommandHandler, FEAT-36).
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));
        _welcomeEmailSenderMock.SendAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Falha simulada no envio do email"));

        // Act
        var act = async () => await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-novo");
        result.Value.AlreadyExisted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotPropagate_WhenWelcomeEmailSenderResolutionFails()
    {
        // Arrange — reproduz o bug real de produção da FEAT-37: a resolução
        // de DI de IWelcomeEmailSender (não só a chamada SendAsync) falha
        // (ex.: cliente SES não consegue resolver a região na Lambda de
        // trigger de conta). GetService retornando null faz
        // GetRequiredService lançar InvalidOperationException — precisa ser
        // capturada pelo try/catch tanto quanto uma falha em SendAsync.
        _accountRepositoryMock.FindAccountIdByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _accountRepositoryMock.CreateAsync("user-1", "user1@email.com", Arg.Any<CancellationToken>())
            .Returns(new CreateAccountResult("account-novo", AlreadyExisted: false));
        _serviceProviderMock.GetService(typeof(IWelcomeEmailSender)).Returns((object?)null);

        // Act
        var act = async () => await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        var result = await _handler.Handle(new EnsureAccountCommand("user-1", "user1@email.com"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.AccountId.Should().Be("account-novo");
        result.Value.AlreadyExisted.Should().BeFalse();
        await _welcomeEmailSenderMock.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }
}
