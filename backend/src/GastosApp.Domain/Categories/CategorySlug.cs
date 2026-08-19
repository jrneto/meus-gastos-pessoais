using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GastosApp.Domain.Categories;

/// <summary>
/// Deriva um slug a partir do nome de uma categoria — usado pela Infrastructure para montar a
/// chave de unicidade (SK) e pelos Validators para rejeitar nomes que não sobram nada depois de
/// normalizados (ex.: "!!!", só emoji).
/// </summary>
public static class CategorySlug
{
    private static readonly Regex NonSlugCharacters = new(@"[^a-z0-9\s-]", RegexOptions.Compiled);
    private static readonly Regex RepeatedSeparators = new(@"[\s-]+", RegexOptions.Compiled);

    public static string From(string nome)
    {
        var normalized = nome.Trim().ToLowerInvariant();
        normalized = RemoveDiacritics(normalized);
        normalized = NonSlugCharacters.Replace(normalized, string.Empty);
        normalized = RepeatedSeparators.Replace(normalized, "-").Trim('-');

        return normalized;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
