using FluentAssertions;
using GastosApp.Application.Accounts.Commands.EnsureAccount;
using GastosApp.Application.Common.Results;
using GastosApp.CognitoTriggers;
using Mediator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.CognitoTriggers;

public class AccountTriggerHandlerTests
{
    private readonly ISender _senderMock;
    private readonly ILogger _loggerMock;

    public AccountTriggerHandlerTests()
    {
        _senderMock = Substitute.For<ISender>();
        _loggerMock = Substitute.For<ILogger>();
    }

    private static CognitoPostConfirmationEvent BuildEvent(string? sub) => new()
    {
        Version = "1",
        Region = "us-east-1",
        UserPoolId = "us-east-1_test",
        UserName = "neto@email.com",
        TriggerSource = "PostConfirmation_ConfirmSignUp",
        Request = new CognitoPostConfirmationRequest
        {
            UserAttributes = sub is null
                ? new Dictionary<string, string> { ["email"] = "neto@email.com" }
                : new Dictionary<string, string> { ["sub"] = sub, ["email"] = "neto@email.com" }
        }
    };

    [Fact]
    public async Task HandleAsync_ShouldDispatchEnsureAccountCommand_WhenSubIsPresent()
    {
        // Arrange
        var evt = BuildEvent("user-sub-123");
        _senderMock.Send(Arg.Any<EnsureAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EnsureAccountResult("account-1", AlreadyExisted: false)));

        // Act
        var result = await AccountTriggerHandler.HandleAsync(evt, _senderMock, _loggerMock, CancellationToken.None);

        // Assert — Cognito exige o evento de volta, alterado ou não.
        result.Should().BeSameAs(evt);
        await _senderMock.Received(1).Send(
            Arg.Is<EnsureAccountCommand>(c => c.UserId == "user-sub-123"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldNotDispatchAnything_WhenSubIsMissing()
    {
        // Arrange — defensivo: Cognito sempre envia "sub", mas nunca deve
        // quebrar a confirmação se por algum motivo não vier.
        var evt = BuildEvent(sub: null);

        // Act
        var result = await AccountTriggerHandler.HandleAsync(evt, _senderMock, _loggerMock, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(evt);
        await _senderMock.DidNotReceiveWithAnyArgs().Send(Arg.Any<EnsureAccountCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldNeverPropagateFailure_WhenEnsureAccountCommandThrows()
    {
        // Arrange — falha transitória do trigger nunca pode impedir a
        // confirmação do cadastro no Cognito (ver spec.md/plan.md, decisão
        // técnica 2): o handler sempre devolve o evento, mesmo sob erro.
        var evt = BuildEvent("user-sub-123");
        _senderMock.Send(Arg.Any<EnsureAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns<Result<EnsureAccountResult>>(_ => throw new InvalidOperationException("Falha simulada no DynamoDB"));

        // Act
        var act = async () => await AccountTriggerHandler.HandleAsync(evt, _senderMock, _loggerMock, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        var result = await AccountTriggerHandler.HandleAsync(evt, _senderMock, _loggerMock, CancellationToken.None);
        result.Should().BeSameAs(evt);
    }
}
