using System.Text.Json.Serialization;

namespace GastosApp.CognitoTriggers.CustomMessage;

// Contexto de serialização gerado em tempo de compilação — obrigatório em
// Native AOT (mesmo motivo de CognitoTriggerJsonSerializerContext em
// GastosApp.CognitoTriggers). PropertyNamingPolicy = CamelCase é obrigatório:
// o Cognito exige receber de volta exatamente os campos que envia, em
// camelCase — sem isso, a resposta sai em PascalCase e o Cognito rejeita com
// InvalidLambdaResponseException ("Unrecognizable lambda output").
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CognitoCustomMessageEvent))]
public partial class CognitoCustomMessageJsonSerializerContext : JsonSerializerContext
{
}
