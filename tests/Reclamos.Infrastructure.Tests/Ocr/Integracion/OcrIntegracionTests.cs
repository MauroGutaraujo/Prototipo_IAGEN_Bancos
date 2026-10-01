using System.Globalization;
using System.Text;

namespace Reclamos.Infrastructure.Tests.Ocr.Integracion;

/// <summary>
/// H4: corre el OCR real sobre 10 vouchers del banco de evaluación (seed 2026) y REPORTA aciertos
/// por campo contra la verdad de referencia. No fija umbrales de desempeño (regla de integridad).
/// </summary>
[Trait("Categoria", "Integracion")]
public class OcrIntegracionTests
{
    private const int Cantidad = 10;

    [Fact]
    public async Task Diez_vouchers_del_banco_de_evaluacion()
    {
        BancoSintetico.OmitirSiNoHayEntorno("data");
        var agente = BancoSintetico.Agente();
        var salida = TestContext.Current.TestOutputHelper!;
        var ct = TestContext.Current.CancellationToken;

        var csv = new StringBuilder("id,monto_ok,fecha_ok,codigo_ok,monto_valido,fecha_valido,codigo_valido,conf_monto,conf_fecha,conf_codigo,error\n");
        var motor = "";
        foreach (var v in BancoSintetico.Vouchers("data", Cantidad))
        {
            var lectura = await agente.ExtraerAsync(v.Ruta, ct);
            motor = $"{lectura.Motor.VersionTesseract} | modelo {lectura.Motor.ModeloSha256}";
            var r = lectura.Resultado;

            // La falla de ejecución sí es un error funcional; los aciertos solo se reportan.
            Assert.Null(lectura.Error);

            var montoOk = r.Monto == v.Monto && r.Moneda == v.Moneda;
            var fechaOk = r.FechaHora is { } f && DateOnly.FromDateTime(f) == v.Fecha;
            var codigoOk = r.Codigo == v.Codigo;
            csv.AppendLine(string.Join(',',
                v.Id, B(montoOk), B(fechaOk), B(codigoOk),
                B(r.Monto is not null && r.ConfMonto >= 0.90m), B(r.FechaHora is not null && r.ConfFecha >= 0.90m),
                B(r.Codigo is not null && r.ConfCodigo >= 0.90m),
                D(r.ConfMonto), D(r.ConfFecha), D(r.ConfCodigo), ""));
            salida.WriteLine($"{v.Id:0000}: monto={B(montoOk)} fecha={B(fechaOk)} codigo={B(codigoOk)}");
        }

        salida.WriteLine($"Motor: {motor}");
        await File.WriteAllTextAsync(Path.Combine(BancoSintetico.DirectorioReportes(), "ocr_integracion.csv"), csv.ToString(), ct);
    }

    private static string B(bool v) => v ? "1" : "0";

    private static string D(decimal v) => v.ToString("0.000", CultureInfo.InvariantCulture);
}
