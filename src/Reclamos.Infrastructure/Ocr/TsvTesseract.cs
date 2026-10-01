using System.Globalization;

namespace Reclamos.Infrastructure.Ocr;

/// <summary>Palabra reconocida con su confianza de Tesseract (0–100).</summary>
public sealed record PalabraOcr(string Texto, decimal Conf);

/// <summary>Renglón de texto en orden de lectura.</summary>
public sealed record LineaOcr(IReadOnlyList<PalabraOcr> Palabras)
{
    public string Texto => string.Join(' ', Palabras.Select(p => p.Texto));
}

/// <summary>Parser de la salida <c>tsv</c> de Tesseract.</summary>
public static class TsvTesseract
{
    private const int NivelPalabra = 5;
    private const int Columnas = 12;

    /// <summary>
    /// Agrupa las palabras (nivel 5) por renglón (página, bloque, párrafo, línea).
    /// Ignora filas con <c>conf = -1</c>, texto vacío o columnas inválidas.
    /// </summary>
    public static IReadOnlyList<LineaOcr> Parsear(string tsv)
    {
        var palabras = new List<(int Pagina, int Bloque, int Parrafo, int Linea, int Palabra, PalabraOcr Valor)>();

        foreach (var fila in tsv.Split('\n').Skip(1))
        {
            var c = fila.TrimEnd('\r').Split('\t');
            if (c.Length < Columnas
                || !int.TryParse(c[0], CultureInfo.InvariantCulture, out var nivel) || nivel != NivelPalabra
                || !decimal.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var conf) || conf < 0
                || string.IsNullOrWhiteSpace(c[11]))
            {
                continue;
            }

            palabras.Add((Entero(c[1]), Entero(c[2]), Entero(c[3]), Entero(c[4]), Entero(c[5]),
                new PalabraOcr(c[11].Trim(), conf)));
        }

        return palabras
            .GroupBy(p => (p.Pagina, p.Bloque, p.Parrafo, p.Linea))
            .OrderBy(g => g.Key)
            .Select(g => new LineaOcr(g.OrderBy(p => p.Palabra).Select(p => p.Valor).ToList()))
            .ToList();
    }

    private static int Entero(string s) => int.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
