using System.Text;
using System.Text.Json;

namespace GastosApp.Application.Common.Cursors;

public static class ExpenseCursorCodec
{
    private static readonly string[] ValidIndexes = ["Base", "GSI1"];

    public static string Encode(ExpenseCursorPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, ExpenseCursorJsonContext.Default.ExpenseCursorPayload);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Base64UrlEncode(bytes);
    }

    public static bool TryDecode(string cursor, out ExpenseCursorPayload? payload)
    {
        payload = null;

        try
        {
            var bytes = Base64UrlDecode(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            var decoded = JsonSerializer.Deserialize(json, ExpenseCursorJsonContext.Default.ExpenseCursorPayload);

            if (decoded is null
                || !ValidIndexes.Contains(decoded.Index)
                || decoded.LastEvaluatedKey is null
                || decoded.LastEvaluatedKey.Count == 0
                || !decoded.LastEvaluatedKey.ContainsKey("PK")
                || !decoded.LastEvaluatedKey.ContainsKey("SK"))
            {
                return false;
            }

            payload = decoded;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or DecoderFallbackException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
