using System.Text.Json.Serialization;

namespace GastosApp.Application.Common.Cursors;

// Contexto de serialização gerado em tempo de compilação — obrigatório porque
// GastosApp.Api publica com PublishAot=true, que desabilita a serialização
// baseada em reflection do System.Text.Json mesmo rodando via `dotnet run`
// local (não só no binário publicado da Lambda). Mesma causa raiz documentada
// em AppJsonSerializerContext (camada Api); TransactionCursorCodec vive na
// camada Application, que não pode depender da Api, daí um contexto próprio aqui.
[JsonSerializable(typeof(TransactionCursorPayload))]
internal sealed partial class TransactionCursorJsonContext : JsonSerializerContext
{
}
