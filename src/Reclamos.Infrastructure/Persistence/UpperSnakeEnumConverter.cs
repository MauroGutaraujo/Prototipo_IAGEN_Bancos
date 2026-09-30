using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Reclamos.Infrastructure.Persistence;

/// <summary>
/// Guarda enums como MAYÚSCULAS_CON_GUION_BAJO, el formato de la SPEC y del banco sintético
/// (p. ej. <c>FueraDeCatalogo</c> ⇄ <c>FUERA_DE_CATALOGO</c>, <c>NoCompletada</c> ⇄ <c>NO_COMPLETADA</c>).
/// </summary>
public sealed class UpperSnakeEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    v => ToUpperSnake(v.ToString()),
    s => FromUpperSnake(s))
    where TEnum : struct, Enum
{
    public static string ToUpperSnake(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(pascal[i - 1]))
                sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    public static TEnum FromUpperSnake(string value) =>
        Enum.Parse<TEnum>(value.Replace("_", string.Empty), ignoreCase: true);
}
