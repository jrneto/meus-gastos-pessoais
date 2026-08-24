using System.Text.Json;
using FluentAssertions;
using GastosApp.CognitoTriggers;
using Xunit;

namespace GastosApp.UnitTests.CognitoTriggers;

// Cobre o shape exato do JSON serializado com o mesmo serializer usado
// pelo Lambda em produção (CognitoTriggerJsonSerializerContext) — os
// testes de AccountTriggerHandlerTests não pegam esse tipo de bug
// porque trabalham com o objeto C#, nunca com o JSON final.
//
// Existe porque quatro bugs reais escaparam pros logs do Cognito em
// homologação (FEAT-19) sem nenhum teste unitário/componente acusar:
// 1) resposta saindo em PascalCase (Cognito exige camelCase de volta)
// 2) campo "callerContext" do evento de entrada sendo descartado
//    silenciosamente na deserialização (POCO não o modelava) e por
//    isso ausente na resposta — Cognito exige o evento completo de
//    volta, não um subconjunto.
// 3) "clientMetadata" ausente na entrada (comum em confirmação manual
//    pelo console AWS, sem app cliente envolvido) virando "null"
//    explícito na saída — Cognito manda o campo ausente, não null, e
//    não reconhece o shape com null de volta.
// 4) "callerContext.clientId" null de verdade na entrada (mesmo cenário
//    de confirmação manual pelo console, sem app cliente — confirmado
//    via log real do CloudWatch, não só inferido da doc) virando "null"
//    explícito na saída pelo mesmo motivo do item 3.
// Os quatro geraram o mesmo InvalidLambdaResponseException
// ("Unrecognizable lambda output"), mesmo com o Lambda executando com
// sucesso (registros criados no banco, sem exceção nos logs).
public class CognitoTriggerJsonSerializerContextTests
{
    private static readonly JsonSerializerOptions Options = CognitoTriggerJsonSerializerContext.Default.Options;

    // JSON real que o Cognito envia pro trigger PostConfirmation (ver
    // docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-post-confirmation.html).
    private const string CognitoRequestJson = """
        {
          "version": "1",
          "region": "us-east-1",
          "userPoolId": "us-east-1_test",
          "userName": "c4c84448-a051-7075-f0f2-792080068ea7",
          "callerContext": {
            "awsSdkVersion": "aws-sdk-unknown-unknown",
            "clientId": "abc123clientid"
          },
          "triggerSource": "PostConfirmation_ConfirmSignUp",
          "request": {
            "userAttributes": {
              "sub": "c4c84448-a051-7075-f0f2-792080068ea7",
              "email": "user@example.com"
            }
          },
          "response": {}
        }
        """;

    [Fact]
    public void RoundTrip_ShouldPreserveEveryTopLevelField_CognitoRequires()
    {
        // Act — mesmo caminho do Lambda: desserializa o evento recebido,
        // devolve exatamente o mesmo objeto (Function.cs/AccountTriggerHandler.cs).
        var evt = JsonSerializer.Deserialize<CognitoPostConfirmationEvent>(CognitoRequestJson, Options);
        var responseJson = JsonSerializer.Serialize(evt, Options);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // Assert — todo campo top-level exigido pelo Cognito precisa
        // estar presente, em camelCase, e não vazio/perdido no round-trip.
        root.TryGetProperty("version", out var version).Should().BeTrue();
        version.GetString().Should().Be("1");

        root.TryGetProperty("region", out _).Should().BeTrue();

        root.TryGetProperty("userPoolId", out _).Should().BeTrue();

        root.TryGetProperty("userName", out var userName).Should().BeTrue();
        userName.GetString().Should().Be("c4c84448-a051-7075-f0f2-792080068ea7");

        root.TryGetProperty("triggerSource", out var triggerSource).Should().BeTrue();
        triggerSource.GetString().Should().Be("PostConfirmation_ConfirmSignUp");

        // callerContext: o campo que escapou na FEAT-19 — precisa
        // sobreviver ao round-trip com seu conteúdo original, não só existir.
        root.TryGetProperty("callerContext", out var callerContext).Should().BeTrue();
        callerContext.GetProperty("awsSdkVersion").GetString().Should().Be("aws-sdk-unknown-unknown");
        callerContext.GetProperty("clientId").GetString().Should().Be("abc123clientid");

        root.TryGetProperty("request", out var request).Should().BeTrue();
        request.GetProperty("userAttributes").GetProperty("sub").GetString()
            .Should().Be("c4c84448-a051-7075-f0f2-792080068ea7");

        // clientMetadata: CognitoRequestJson acima não manda esse campo
        // (cenário real de confirmação manual pelo console) — precisa
        // continuar ausente na resposta, nunca virar "null" explícito.
        request.TryGetProperty("clientMetadata", out _).Should().BeFalse(
            "o Cognito manda o campo ausente quando não há client metadata, e rejeita \"clientMetadata\": null de volta");

        root.TryGetProperty("response", out _).Should().BeTrue();
    }

    // JSON real capturado do log de diagnóstico em homologação (FEAT-19,
    // usuário confirmado manualmente pelo console AWS) — não um exemplo
    // construído a partir da doc. callerContext.clientId vem null de
    // verdade nesse cenário (sem app cliente envolvido).
    private const string CognitoConsoleConfirmRequestJson = """
        {
          "version": "1",
          "region": "us-east-1",
          "userPoolId": "us-east-1_GFfn9AMAN",
          "userName": "44688458-a001-70b5-53f3-26ea2bd67f8b",
          "callerContext": {
            "awsSdkVersion": "aws-sdk-js-2.1639.0",
            "clientId": null
          },
          "triggerSource": "PostConfirmation_ConfirmSignUp",
          "request": {
            "userAttributes": {
              "sub": "44688458-a001-70b5-53f3-26ea2bd67f8b",
              "email_verified": "false",
              "cognito:user_status": "CONFIRMED",
              "email": "user@example.com"
            }
          },
          "response": {}
        }
        """;

    [Fact]
    public void RoundTrip_ShouldOmitClientId_WhenConsoleConfirmSendsItNull()
    {
        // Act
        var evt = JsonSerializer.Deserialize<CognitoPostConfirmationEvent>(CognitoConsoleConfirmRequestJson, Options);
        var responseJson = JsonSerializer.Serialize(evt, Options);

        using var doc = JsonDocument.Parse(responseJson);
        var callerContext = doc.RootElement.GetProperty("callerContext");

        // Assert
        callerContext.GetProperty("awsSdkVersion").GetString().Should().Be("aws-sdk-js-2.1639.0");
        callerContext.TryGetProperty("clientId", out _).Should().BeFalse(
            "o Cognito manda clientId null quando não há app cliente envolvido, e rejeita \"clientId\": null de volta");
    }
}
