using System.Text.Json.Serialization;

namespace GastosApp.CognitoTriggers.CustomMessage;

// Não existe pacote oficial da AWS com tipos para User Pool Lambda triggers em
// .NET (mesmo achado da FEAT-19, ver CognitoPostConfirmationEvent.cs em
// GastosApp.CognitoTriggers) — POCO próprio, formato documentado em
// docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-message.html.
// Cobre os TriggerSource CustomMessage_SignUp, CustomMessage_ResendCode e
// CustomMessage_ForgotPassword (únicos tratados por esta feature — ver spec.md);
// os demais (ex.: CustomMessage_AdminCreateUser) chegam no mesmo formato, mas o
// handler devolve Response sem alterar.
public sealed class CognitoCustomMessageEvent
{
    public string Version { get; set; } = "";
    public string Region { get; set; } = "";
    public string UserPoolId { get; set; } = "";
    public string UserName { get; set; } = "";
    public CognitoCustomMessageCallerContext CallerContext { get; set; } = new();
    public string TriggerSource { get; set; } = "";
    public CognitoCustomMessageRequest Request { get; set; } = new();
    public CognitoCustomMessageResponse Response { get; set; } = new();
}

public sealed class CognitoCustomMessageCallerContext
{
    public string AwsSdkVersion { get; set; } = "";

    // Mesmo gotcha já documentado na FEAT-19 (CognitoPostConfirmationEvent.cs):
    // confirmado que o Cognito pode mandar este campo null (ex.: chamada sem
    // app cliente envolvido) — sem WhenWritingNull, o round-trip devolve
    // "clientId":null explícito e o Cognito rejeita com
    // InvalidLambdaResponseException.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; set; }
}

public sealed class CognitoCustomMessageRequest
{
    public Dictionary<string, string> UserAttributes { get; set; } = new();

    // Literal "{####}" — NÃO é o código real gerado pelo Cognito. O handler só
    // precisa reposicionar esse token onde {{codigo}} aparece (corpo e
    // assunto); o Cognito substitui pelo código de verdade depois que o
    // Lambda retorna (ver plan.md, decisão técnica 1).
    public string CodeParameter { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UsernameParameter { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? ClientMetadata { get; set; }
}

public sealed class CognitoCustomMessageResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SmsMessage { get; set; }
    public string? EmailMessage { get; set; }
    public string? EmailSubject { get; set; }
}
