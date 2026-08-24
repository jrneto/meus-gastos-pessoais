namespace GastosApp.CognitoTriggers;

// Não existe pacote oficial da AWS com tipos para User Pool Lambda
// triggers em .NET (Amazon.Lambda.CognitoEvents é Cognito Sync, serviço
// distinto — verificado antes de assumir, ver plan.md). POCO próprio,
// modelando o formato documentado pela AWS em
// docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-post-confirmation.html.
// Cognito invoca esse trigger pra ConfirmSignUp, AdminConfirmSignUp e
// ConfirmForgotPassword (3 valores de TriggerSource) — EnsureAccountCommand
// é idempotente nos três casos, não precisa distinguir entre eles.
public sealed class CognitoPostConfirmationEvent
{
    public string Version { get; set; } = "";
    public string Region { get; set; } = "";
    public string UserPoolId { get; set; } = "";
    public string UserName { get; set; } = "";

    // Precisa existir no POCO mesmo sem o handler usar seu conteúdo: o
    // Cognito exige o evento de volta *completo* (documentado junto com
    // version/region/userPoolId etc.), e o System.Text.Json ignora
    // silenciosamente campos desconhecidos na deserialização — sem esta
    // propriedade, callerContext simplesmente desaparecia na resposta.
    // Causou InvalidLambdaResponseException ("Unrecognizable lambda
    // output") mesmo após corrigir o casing (camelCase) do restante do
    // evento — achado real ao validar a FEAT-19 em homologação.
    public CognitoPostConfirmationCallerContext CallerContext { get; set; } = new();

    public string TriggerSource { get; set; } = "";
    public CognitoPostConfirmationRequest Request { get; set; } = new();

    // "No additional return information is expected in the response" — a AWS
    // nunca lê nada daqui pra este trigger, só exige a chave de volta no
    // objeto (dict de string evita problema de tipo polimórfico (object) no
    // JsonSerializerContext source-generated, obrigatório sob Native AOT).
    public Dictionary<string, string> Response { get; set; } = new();
}

public sealed class CognitoPostConfirmationCallerContext
{
    public string AwsSdkVersion { get; set; } = "";
    public string ClientId { get; set; } = "";
}

public sealed class CognitoPostConfirmationRequest
{
    public Dictionary<string, string> UserAttributes { get; set; } = new();
    public Dictionary<string, string>? ClientMetadata { get; set; }
}
