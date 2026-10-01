using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Reclamos.Guardrails;

internal static class Normalizacion
{
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Código de operación en mayúsculas y sin espacios.</summary>
    public static string Codigo(string codigo) =>
        string.Concat(codigo.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    /// <summary>Texto en minúsculas, sin tildes y con espacios simples (para buscar frases).</summary>
    public static string Texto(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return Espacios.Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ").Trim().ToLowerInvariant();
    }
}
