using System.Globalization;
using System.Text;
using Reclamos.Infrastructure.Ocr;

namespace Reclamos.Infrastructure.Tests.Ocr.Integracion;

/// <summary>
/// Ajuste del preprocesamiento SOLO con el banco de desarrollo (seed 7, carpeta data-dev).
/// Nunca usa el banco de evaluación (seed 2026). Reporta aciertos por configuración; no elige.
/// </summary>
[Trait("Categoria", "AjusteOcr")]
public class AjusteOcrTests
{
    private const int Cantidad = 60;
    private static readonly Lock Archivo = new();

    public static TheoryData<string> Configuraciones() => [.. Grilla().Keys];

    private static Dictionary<string, PreprocesamientoOptions> Grilla()
    {
        var grilla = new Dictionary<string, PreprocesamientoOptions>();
        foreach (var escala in new[] { 1f, 1.5f })
        foreach (var enderezar in new[] { false, true })
        foreach (var modo in Enum.GetValues<ModoBinarizacion>())
        {
            var nombre = $"escala={escala.ToString(CultureInfo.InvariantCulture)};enderezar={enderezar};bin={modo}";
            grilla[nombre] = new PreprocesamientoOptions { Escala = escala, Enderezar = enderezar, Binarizacion = modo };
        }
        return grilla;
    }

    [Theory]
    [MemberData(nameof(Configuraciones))]
    public async Task Banco_de_desarrollo(string configuracion)
    {
        BancoSintetico.OmitirSiNoHayEntorno("data-dev");
        var agente = BancoSintetico.Agente(Grilla()[configuracion]);
        var ct = TestContext.Current.CancellationToken;

        int monto = 0, fecha = 0, codigo = 0, validos = 0, n = 0;
        foreach (var v in BancoSintetico.Vouchers("data-dev", Cantidad))
        {
            var r = (await agente.ExtraerAsync(v.Ruta, ct)).Resultado;
            n++;
            var m = r.Monto == v.Monto && r.Moneda == v.Moneda;
            var f = r.FechaHora is { } fh && DateOnly.FromDateTime(fh) == v.Fecha;
            var c = r.Codigo == v.Codigo;
            monto += m ? 1 : 0;
            fecha += f ? 1 : 0;
            codigo += c ? 1 : 0;
            validos += m && f && c && r.ConfMonto >= 0.90m && r.ConfFecha >= 0.90m && r.ConfCodigo >= 0.90m ? 1 : 0;
        }

        var archivo = Path.Combine(BancoSintetico.DirectorioReportes(), "ocr_ajuste_dev.csv");
        lock (Archivo)
        {
            if (!File.Exists(archivo))
                File.WriteAllText(archivo, "configuracion,n,monto_ok,fecha_ok,codigo_ok,tres_ok_y_validos\n");
            File.AppendAllText(archivo, $"\"{configuracion}\",{n},{monto},{fecha},{codigo},{validos}\n", Encoding.UTF8);
        }
    }
}
