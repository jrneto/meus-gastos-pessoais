using System.Text.Json.Serialization;

namespace GastosApp.CognitoTriggers;

// Contexto de serialização gerado em tempo de compilação — obrigatório
// em Native AOT (mesmo motivo do AppJsonSerializerContext em GastosApp.Api).
//
// PropertyNamingPolicy = CamelCase é obrigatório aqui: o Cognito exige
// receber de volta exatamente os campos que envia (version, region,
// userPoolId, userName, triggerSource, request.userAttributes,
// request.clientMetadata, response), em camelCase. Sem isso, a
// serialização usa o nome literal das propriedades C# (PascalCase) e o
// Cognito rejeita a resposta com InvalidLambdaResponseException
// ("Unrecognizable lambda output") mesmo com o Lambda executando com
// sucesso — a deserialização do evento de entrada não expõe o problema
// porque o serializer da AWS já é case-insensitive nesse sentido.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CognitoPostConfirmationEvent))]
public partial class CognitoTriggerJsonSerializerContext : JsonSerializerContext
{
}
