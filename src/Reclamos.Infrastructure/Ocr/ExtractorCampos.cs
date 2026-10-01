using System.Globalization;
using System.Text.RegularExpressions;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;

namespace Reclamos.Infrastructure.Ocr;

/// <summary>
/// Extrae monto, fecha/hora y código de los renglones OCR (SPEC §2.1). Puro y determinista.
/// c_f = media de conf/100 de las palabras del valor, redondeada a 3 decimales (half away from zero).
/// </summary>
public static class ExtractorCampos
{
    private const RegexOptions Opc = RegexOptions.CultureInvariant;

    private static readonly Regex Monto = new(@"(S/|US\$)\s?(\d{1,3}(?:,\d{3})*\.\d{2})(?!\d)", Opc);
    private static readonly Regex Fecha = new(@"(?<!\d)(\d{2})[/-](\d{2})[/-](\d{4})(?!\d)", Opc);
    private static readonly Regex Hora = new(@"(?<!\d)([01]\d|2[0-3]):([0-5]\d)(?!\d)", Opc);
    private static readonly Regex Codigo = new(
        @"(?:Operaci[oó]n|C[oó]d\.?\s*operaci[oó]n)\s*:?\s*([A-Z0-9]{8,12})(?![A-Z0-9])", Opc | RegexOptions.IgnoreCase);

    public static ResultadoOcr Extraer(IReadOnlyList<LineaOcr> lineas)
    {
        decimal? monto = null;
        Moneda? moneda = null;
        var confMonto = 0m;
        var m = Buscar(lineas, Monto, _ => true);
        if (m is { } mm)
        {
            moneda = mm.Match.Groups[1].Value == "S/" ? Moneda.PEN : Moneda.USD;
            monto = decimal.Parse(mm.Match.Groups[2].Value.Replace(",", ""), CultureInfo.InvariantCulture);
            confMonto = Confianza(mm.Linea, mm.Match.Index, mm.Match.Length);
        }

        DateTime? fechaHora = null;
        var confFecha = 0m;
        var f = Buscar(lineas, Fecha, x => FechaValida(x) is not null);
        if (f is { } ff)
        {
            var palabras = Palabras(ff.Linea, ff.Match.Index, ff.Match.Length);
            var dia = FechaValida(ff.Match)!.Value;
            fechaHora = dia;
            if (Buscar(lineas, Hora, _ => true) is { } h)
            {
                fechaHora = dia.AddHours(int.Parse(h.Match.Groups[1].Value, CultureInfo.InvariantCulture))
                    .AddMinutes(int.Parse(h.Match.Groups[2].Value, CultureInfo.InvariantCulture));
                palabras.AddRange(Palabras(h.Linea, h.Match.Index, h.Match.Length));
            }
            confFecha = Media(palabras);
        }

        string? codigo = null;
        var confCodigo = 0m;
        var c = Buscar(lineas, Codigo, _ => true);
        if (c is { } cc)
        {
            var g = cc.Match.Groups[1];
            codigo = g.Value.ToUpperInvariant();
            confCodigo = Confianza(cc.Linea, g.Index, g.Length);
        }

        return new ResultadoOcr(monto, moneda, fechaHora, codigo, confMonto, confFecha, confCodigo);
    }

    private static (LineaOcr Linea, Match Match)? Buscar(IReadOnlyList<LineaOcr> lineas, Regex regex, Func<Match, bool> aceptar)
    {
        foreach (var linea in lineas)
        {
            foreach (Match m in regex.Matches(linea.Texto))
            {
                if (aceptar(m))
                    return (linea, m);
            }
        }
        return null;
    }

    private static DateTime? FechaValida(Match m)
    {
        var dia = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mes = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var anio = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        return mes is >= 1 and <= 12 && anio >= 1 && dia >= 1 && dia <= DateTime.DaysInMonth(anio, mes)
            ? new DateTime(anio, mes, dia)
            : null;
    }

    /// <summary>Palabras del renglón que se superponen con [inicio, inicio + largo).</summary>
    private static List<PalabraOcr> Palabras(LineaOcr linea, int inicio, int largo)
    {
        var resultado = new List<PalabraOcr>();
        var pos = 0;
        foreach (var p in linea.Palabras)
        {
            var fin = pos + p.Texto.Length;
            if (pos < inicio + largo && fin > inicio)
                resultado.Add(p);
            pos = fin + 1; // separador ' '
        }
        return resultado;
    }

    private static decimal Confianza(LineaOcr linea, int inicio, int largo) => Media(Palabras(linea, inicio, largo));

    private static decimal Media(List<PalabraOcr> palabras) =>
        Math.Round(palabras.Average(p => p.Conf) / 100m, 3, MidpointRounding.AwayFromZero);
}
