using System.Text.Json.Serialization;

namespace GastosApp.CognitoTriggers;

// Contexto de serialização gerado em tempo de compilação — obrigatório
// em Native AOT (mesmo motivo do AppJsonSerializerContext em GastosApp.Api).
[JsonSerializable(typeof(CognitoPostConfirmationEvent))]
public partial class CognitoTriggerJsonSerializerContext : JsonSerializerContext
{
}
