using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Reclamos.Domain.Enums;
using Reclamos.Infrastructure.Ocr;

namespace Reclamos.Infrastructure.Tests.Ocr.Integracion;

/// <summary>Verdad de referencia de un voucher (lo que está impreso).</summary>
internal sealed record VoucherEsperado(int Id, string Ruta, decimal Monto, Moneda Moneda, DateOnly Fecha, string Codigo);

/// <summary>Acceso al banco sintético generado por tools/dataset-generator (solo para tests de evaluación).</summary>
internal static class BancoSintetico
{
    public static string RaizRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Reclamos.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio");
    }

    public static string Datos(string carpeta) => Path.Combine(RaizRepo(), "tools", "dataset-generator", carpeta);

    /// <summary>Carpeta del modelo: variable TESSDATA_DIR (la fija CI).</summary>
    public static string? TessdataDir() => Environment.GetEnvironmentVariable("TESSDATA_DIR");

    public static string DirectorioReportes()
    {
        var dir = Environment.GetEnvironmentVariable("OCR_REPORTES_DIR") ?? Path.Combine(RaizRepo(), "TestResults", "ocr");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void OmitirSiNoHayEntorno(string carpetaDatos)
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(TessdataDir()), "TESSDATA_DIR no definido (Tesseract no disponible)");
        Assert.SkipUnless(File.Exists(Path.Combine(Datos(carpetaDatos), "verdad_referencia.csv")),
            $"No existe tools/dataset-generator/{carpetaDatos}: genera el banco primero");
    }

    public static TesseractCliOcrAgent Agente(PreprocesamientoOptions? pre = null) =>
        new(Options.Create(new OcrOptions
        {
            TessdataDir = TessdataDir()!,
            Preprocesamiento = pre ?? new PreprocesamientoOptions(),
        }));

    public static IReadOnlyList<VoucherEsperado> Vouchers(string carpetaDatos, int cantidad)
    {
        var dir = Datos(carpetaDatos);
        return LeerCsv(Path.Combine(dir, "verdad_referencia.csv"))
            .Select(f => new VoucherEsperado(
                int.Parse(f["id_expediente"], CultureInfo.InvariantCulture),
                Path.Combine(dir, "vouchers", $"{int.Parse(f["id_expediente"], CultureInfo.InvariantCulture):0000}.jpg"),
                decimal.Parse(f["monto_real"], CultureInfo.InvariantCulture),
                Enum.Parse<Moneda>(f["moneda_real"]),
                DateOnly.FromDateTime(DateTime.ParseExact(f["fecha_real"], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                f["codigo_real"]))
            .OrderBy(v => v.Id)
            .Take(cantidad)
            .ToList();
    }

    /// <summary>CSV RFC 4180 mínimo (campos entre comillas con comas).</summary>
    private static List<Dictionary<string, string>> LeerCsv(string ruta)
    {
        var filas = File.ReadAllLines(ruta, Encoding.UTF8).Where(l => l.Length > 0).Select(Campos).ToList();
        var encabezado = filas[0];
        return filas.Skip(1)
            .Select(c => encabezado.Zip(c).ToDictionary(p => p.First, p => p.Second))
            .ToList();
    }

    private static List<string> Campos(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var comillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var ch = linea[i];
            if (comillas)
            {
                if (ch == '"' && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else if (ch == '"') comillas = false;
                else actual.Append(ch);
            }
            else if (ch == '"') comillas = true;
            else if (ch == ',') { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(ch);
        }
        campos.Add(actual.ToString());
        return campos;
    }
}
