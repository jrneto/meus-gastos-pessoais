using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace GastosApp.Api.Common;

// Contexto de serialização para os eventos do Lambda/API Gateway em si
// (a "camada de transporte" do hosting, distinta do AppJsonSerializerContext
// que cobre os DTOs da aplicação). Também obrigatório em Native AOT —
// achado durante a implementação da FEAT-10: sem isso,
// Amazon.Lambda.AspNetCoreServer.Hosting lança
// "Reflection-based serialization has been disabled" ao tentar
// desserializar o evento recebido do API Gateway.
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
public partial class LambdaEventJsonSerializerContext : JsonSerializerContext
{
}