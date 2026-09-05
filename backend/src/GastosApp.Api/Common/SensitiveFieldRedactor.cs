using System.Buffers;
using System.Text;
using System.Text.Json;

namespace GastosApp.Api.Common;

// Redação de campos sensíveis num corpo JSON cru, antes de entrar no log
// (FEAT-38). Usa JsonDocument/Utf8JsonWriter (API de DOM, sem
// reflection/source-generator) — segura sob Native AOT, diferente de
// JsonSerializer.Deserialize<T> sem JsonTypeInfo explícito: o corpo aqui
// tem formato arbitrário (qualquer request/response da API), não um DTO
// conhecido do AppJsonSerializerContext.
public static class SensitiveFieldRedactor
{
    private const string Mask = "***";

    // Nomes de propriedade JSON (case-insensitive) nunca logados em
    // texto puro — lista fechada, fácil de estender depois.
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "oldPassword", "code",
        "token", "accessToken", "refreshToken", "idToken",
        "cardNumber", "cvv"
    };

    public static string Redact(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Não é um JSON válido — devolve como veio. Quem chama
            // (RequestLogEntryBuilder) já filtrou por Content-Type antes
            // de chegar aqui; isso é só uma rede de segurança.
            return json;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object && root.ValueKind != JsonValueKind.Array)
            {
                // JSON válido, mas não é um objeto/array (ex.: só um
                // número ou string) — nada a redigir, devolve como veio.
                return json;
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteRedacted(writer, root);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
    }

    private static void WriteRedacted(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (SensitiveFields.Contains(property.Name))
                    {
                        writer.WriteStringValue(Mask);
                    }
                    else
                    {
                        WriteRedacted(writer, property.Value);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRedacted(writer, item);
                }
                writer.WriteEndArray();
                break;

            default:
                // Escalar (string, número, bool, null) — copiado verbatim.
                element.WriteTo(writer);
                break;
        }
    }
}
