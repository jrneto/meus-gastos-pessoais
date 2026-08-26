using System.Globalization;
using System.Text;

namespace GastosApp.Application.Transactions.Queries.ExportTransactions;

public sealed record ExportTransactionRow(
    DateOnly Date,
    string Description,
    string CategoryNome,
    string Tipo,
    long AmountInCents,
    string CreatedByLabel);

// Formatter puro (sem I/O) — testável isoladamente, sem mockar repositório.
// Colunas pensadas pra abrir direto numa planilha (spec.md, decisões de
// escopo 2-5): nome de categoria (não id), valor em reais com vírgula
// decimal (não centavos), delimitador ";" (não ",", que já é o separador
// decimal), UTF-8 com BOM, escaping RFC 4180.
public static class TransactionCsvBuilder
{
    private const string Delimiter = ";";
    private const string NewLine = "\r\n";
    private static readonly string[] Header = ["data", "descricao", "categoria", "tipo", "valor", "lancadoPor"];

    public static byte[] Build(IReadOnlyList<ExportTransactionRow> rows)
    {
        var lines = new List<string> { string.Join(Delimiter, Header) };
        lines.AddRange(rows.Select(BuildRow));
        var content = string.Join(NewLine, lines) + NewLine;

        // GetBytes() nunca inclui o preamble, mesmo com encoderShouldEmitUTF8Identifier:
        // true — esse flag só afeta o retorno de GetPreamble(). É preciso concatenar os
        // dois manualmente pra gravar o BOM (EF BB BF) no início do arquivo, necessário
        // pro Excel reconhecer acentuação sem pedir a codificação manualmente (spec.md,
        // decisão de escopo 4).
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = encoding.GetPreamble();
        var contentBytes = encoding.GetBytes(content);

        var bytes = new byte[preamble.Length + contentBytes.Length];
        preamble.CopyTo(bytes, 0);
        contentBytes.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static string BuildRow(ExportTransactionRow row) => string.Join(Delimiter,
    [
        row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Escape(row.Description),
        Escape(row.CategoryNome),
        row.Tipo,
        FormatValor(row.AmountInCents),
        Escape(row.CreatedByLabel)
    ]);

    // Única exceção à convenção "sempre centavos" do projeto — este é o único
    // ponto da API pensado pra consumo humano direto (spec.md, decisão de
    // escopo 2). "0.00" (invariant) nunca usa separador de milhar; troca só o
    // separador decimal "." por "," (padrão pt-BR).
    private static string FormatValor(long amountInCents) =>
        (amountInCents / 100m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Escape(string field)
    {
        var needsQuoting = field.Contains(Delimiter, StringComparison.Ordinal)
            || field.Contains('"')
            || field.Contains('\n')
            || field.Contains('\r');

        return needsQuoting ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
