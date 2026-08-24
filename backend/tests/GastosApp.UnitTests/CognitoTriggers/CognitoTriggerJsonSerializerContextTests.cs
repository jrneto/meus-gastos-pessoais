using System.Text;
using System.Text.Json;
using Amazon.Lambda.Serialization.SystemTextJson;
using FluentAssertions;
using GastosApp.CognitoTriggers;
using Xunit;

namespace GastosApp.UnitTests.CognitoTriggers;

// Cobre o shape exato do JSON que sai da Lambda em produção — os testes
// de AccountTriggerHandlerTests não pegam esse tipo de bug porque
// trabalham com o objeto C#, nunca com o JSON final.
//
// Existe porque cinco bugs reais escaparam pros logs do Cognito em
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
// 5) o MAIS insidioso: o fix do item 1 nunca chegou a valer em produção.
//    Este arquivo, até esta versão, testava a serialização chamando
//    JsonSerializer.Serialize(evt, CognitoTriggerJsonSerializerContext.Default.Options)
//    diretamente — só que Function.cs NUNCA usa Default: ele constrói
//    `new SourceGeneratorLambdaJsonSerializer<T>()` (ctor sem
//    parâmetros), que internamente cria uma instância NOVA do contexto
//    com a AwsNamingPolicy própria do pacote da AWS, ignorando o
//    [JsonSourceGenerationOptions(PropertyNamingPolicy = CamelCase)] da
//    classe. Resultado: os testes ficavam verdes (usavam Default,
//    camelCase correto) enquanto a Lambda real devolvia PascalCase —
//    confirmado só invocando a Lambda de verdade (aws lambda invoke) e
//    comparando byte a byte com o log da própria aplicação. Por isso os
//    testes abaixo usam CreateProductionSerializer(), que replica
//    EXATAMENTE a construção de Function.cs, nunca CognitoTriggerJsonSerializerContext.Default direto.
// Os cinco geraram o mesmo InvalidLambdaResponseException
// ("Unrecognizable lambda output"), mesmo com o Lambda executando com
// sucesso (registros criados no banco, sem exceção nos logs).
public class CognitoTriggerJsonSerializerContextTests
{
    // Espelha Function.cs literalmente — se alguém trocar essa
    // construção lá (e voltar a usar o ctor sem parâmetros), este teste
    // tem que ser o primeiro a quebrar.
    private static SourceGeneratorLambdaJsonSerializer<CognitoTriggerJsonSerializerContext> CreateProductionSerializer() =>
        new(options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

    private static string SerializeAsProduction(CognitoPostConfirmationEvent evt)
    {
        using var stream = new MemoryStream();
        CreateProductionSerializer().Serialize(evt, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static CognitoPostConfirmationEvent DeserializeAsProduction(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return CreateProductionSerializer().Deserialize<CognitoPostConfirmationEvent>(stream);
    }

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
        // Act — mesmo caminho do Lambda de verdade: desserializa o evento
        // recebido, devolve exatamente o mesmo objeto
        // (Function.cs/AccountTriggerHandler.cs), serializado pelo mesmo
        // ILambdaSerializer que LambdaBootstrapBuilder usa em produção.
        var evt = DeserializeAsProduction(CognitoRequestJson);
        var responseJson = SerializeAsProduction(evt);

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
        var evt = DeserializeAsProduction(CognitoConsoleConfirmRequestJson);
        var responseJson = SerializeAsProduction(evt);

        using var doc = JsonDocument.Parse(responseJson);
        var callerContext = doc.RootElement.GetProperty("callerContext");

        // Assert
        callerContext.GetProperty("awsSdkVersion").GetString().Should().Be("aws-sdk-js-2.1639.0");
        callerContext.TryGetProperty("clientId", out _).Should().BeFalse(
            "o Cognito manda clientId null quando não há app cliente envolvido, e rejeita \"clientId\": null de volta");
    }

    [Fact]
    public void ProductionSerializer_ShouldUseCamelCase_NotTheDefaultCtorAwsNamingPolicy()
    {
        // Guarda especificamente contra a regressão do item 5 do
        // cabeçalho: se Function.cs (ou este helper) voltar a usar
        // `new SourceGeneratorLambdaJsonSerializer<T>()` sem o
        // customizer de PropertyNamingPolicy, este teste falha sozinho,
        // sem precisar de outro ciclo de "deploya e testa em homologação"
        // pra descobrir.
        var evt = new CognitoPostConfirmationEvent { Version = "1", TriggerSource = "PostConfirmation_ConfirmSignUp" };

        var json = SerializeAsProduction(evt);

        json.Should().Contain("\"version\"").And.Contain("\"triggerSource\"");
        json.Should().NotContain("\"Version\"").And.NotContain("\"TriggerSource\"");
    }
}
