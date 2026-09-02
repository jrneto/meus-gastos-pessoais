using FluentAssertions;
using GastosApp.CognitoTriggers.CustomMessage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.CognitoTriggers;

public class CustomMessageTriggerHandlerTests
{
    private readonly ILogger _loggerMock;

    public CustomMessageTriggerHandlerTests()
    {
        _loggerMock = Substitute.For<ILogger>();
    }

    private static CognitoCustomMessageEvent BuildEvent(
        string triggerSource, string? name = "Fulano da Silva", string? email = "neto@email.com") => new()
    {
        Version = "1",
        Region = "us-east-1",
        UserPoolId = "us-east-1_test",
        UserName = "neto@email.com",
        TriggerSource = triggerSource,
        Request = new CognitoCustomMessageRequest
        {
            CodeParameter = "{####}", // literal do Cognito — nunca o código real
            UserAttributes = BuildAttributes(name, email)
        }
    };

    private static Dictionary<string, string> BuildAttributes(string? name, string? email)
    {
        var attributes = new Dictionary<string, string>();
        if (name is not null)
            attributes["name"] = name;
        if (email is not null)
            attributes["email"] = email;
        return attributes;
    }

    [Theory]
    [InlineData("CustomMessage_SignUp")]
    [InlineData("CustomMessage_ResendCode")]
    public async Task HandleAsync_ShouldFillSignUpTemplate_ForSignUpAndResendCode(string triggerSource)
    {
        // Arrange
        var evt = BuildEvent(triggerSource);

        // Act
        var result = await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(evt);
        result.Response.EmailMessage.Should().Contain("Fulano da Silva").And.Contain("{####}").And.Contain("neto@email.com");
        result.Response.EmailMessage.Should().NotContain("{{nome}}").And.NotContain("{{codigo}}").And.NotContain("{{email}}");
        // Assunto não usa "{{codigo}}"/"{####}" — confirmado ao vivo em hom
        // que o Cognito não substitui o placeholder em emailSubject (só em
        // emailMessage), diferente do assumido originalmente no plan.md.
        result.Response.EmailSubject.Should().Be("Confirme seu cadastro no jrn.expenses");
    }

    [Fact]
    public async Task HandleAsync_ShouldFillForgotPasswordTemplate_ForForgotPassword()
    {
        // Arrange
        var evt = BuildEvent("CustomMessage_ForgotPassword");

        // Act
        var result = await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);

        // Assert
        result.Response.EmailMessage.Should().Contain("Redefinir sua senha").And.Contain("{####}").And.Contain("neto@email.com");
        result.Response.EmailSubject.Should().Be("Redefinição de senha solicitada");
    }

    [Fact]
    public async Task HandleAsync_ShouldUseFallbackGreeting_WhenNameAttributeIsMissing()
    {
        // Arrange — defensivo (Requisitos do spec.md): nunca deixa "{{nome}}"
        // literal no e-mail se o atributo "name" não vier no evento.
        var evt = BuildEvent("CustomMessage_SignUp", name: null);

        // Act
        var result = await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);

        // Assert
        result.Response.EmailMessage.Should().Contain("Olá,").And.NotContain("{{nome}}");
    }

    [Fact]
    public async Task HandleAsync_ShouldNotChangeResponse_ForOutOfScopeTriggerSource()
    {
        // Arrange — ex.: CustomMessage_AdminCreateUser, fora do escopo desta
        // feature (US5 do spec.md) — Cognito deve usar o texto padrão dele.
        var evt = BuildEvent("CustomMessage_AdminCreateUser");

        // Act
        var result = await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(evt);
        result.Response.EmailMessage.Should().BeNull();
        result.Response.EmailSubject.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldNeverPropagateFailure_WhenFormattingThrows()
    {
        // Arrange — falha inesperada nunca pode impedir o SignUp/ResendCode/
        // ForgotPassword em andamento (US4 do spec.md): o handler sempre
        // devolve o evento, mesmo sob erro, e o Cognito cai no texto padrão
        // dele (Response não é alterado).
        var evt = BuildEvent("CustomMessage_SignUp");
        evt.Request = null!; // força NullReferenceException ao acessar Request.UserAttributes

        // Act
        var act = async () => await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        var result = await CustomMessageTriggerHandler.HandleAsync(evt, _loggerMock, CancellationToken.None);
        result.Should().BeSameAs(evt);
        result.Response.EmailMessage.Should().BeNull();
    }
}
