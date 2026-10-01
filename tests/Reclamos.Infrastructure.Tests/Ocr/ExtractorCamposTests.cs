using Reclamos.Domain.Enums;
using Reclamos.Infrastructure.Ocr;

namespace Reclamos.Infrastructure.Tests.Ocr;

public class ExtractorCamposTests
{
    private static LineaOcr L(params (string Texto, double Conf)[] palabras) =>
        new(palabras.Select(p => new PalabraOcr(p.Texto, (decimal)p.Conf)).ToList());

    private static readonly LineaOcr[] Comprobante =
    [
        L(("BANCO", 95), ("ANDINO", 95)),
        L(("CONSTANCIA", 94), ("DE", 94), ("OPERACIÓN", 94)),
        L(("Fecha:", 96), ("26/01/2026", 92)),
        L(("Hora:", 96), ("19:40", 90)),
        L(("Monto:", 97), ("S/", 98), ("1,040.33", 94)),
        L(("Cód.", 90), ("operación:", 91), ("AN7QFKBK01", 96)),
        L(("Estado:", 95), ("Aprobada", 95)),
    ];

    [Fact]
    public void Extrae_los_tres_campos_de_un_comprobante()
    {
        var r = ExtractorCampos.Extraer(Comprobante);

        Assert.Equal(1040.33m, r.Monto);
        Assert.Equal(Moneda.PEN, r.Moneda);
        Assert.Equal(new DateTime(2026, 1, 26, 19, 40, 0), r.FechaHora);
        Assert.Equal("AN7QFKBK01", r.Codigo);
        Assert.Equal(0.96m, r.ConfMonto);   // (98 + 94) / 2 / 100
        Assert.Equal(0.91m, r.ConfFecha);   // (92 + 90) / 2 / 100
        Assert.Equal(0.96m, r.ConfCodigo);  // solo la palabra del código
    }

    [Fact]
    public void Monto_en_dolares_sin_espacio()
    {
        var r = ExtractorCampos.Extraer([L(("US$45.00", 93))]);
        Assert.Equal((45.00m, Moneda.USD, 0.93m), (r.Monto, r.Moneda, r.ConfMonto));
    }

    [Theory]
    [InlineData("S/ 150")]       // sin decimales
    [InlineData("S/ 1234.50")]   // sin separador de miles
    [InlineData("150.50")]       // sin símbolo
    public void Monto_fuera_de_formato_no_se_extrae(string texto)
    {
        var r = ExtractorCampos.Extraer([L(texto.Split(' ').Select(t => (t, 99.0)).ToArray())]);
        Assert.Null(r.Monto);
        Assert.Null(r.Moneda);
        Assert.Equal(0m, r.ConfMonto);
    }

    [Fact]
    public void Se_toma_la_primera_aparicion()
    {
        var r = ExtractorCampos.Extraer([L(("S/", 90), ("10.00", 90)), L(("S/", 99), ("20.00", 99))]);
        Assert.Equal(10.00m, r.Monto);
    }

    [Fact]
    public void Fecha_con_guiones_y_sin_hora_queda_a_medianoche()
    {
        var r = ExtractorCampos.Extraer([L(("01-06-2026", 95))]);
        Assert.Equal(new DateTime(2026, 6, 1), r.FechaHora);
        Assert.Equal(0.95m, r.ConfFecha);
    }

    [Fact]
    public void Fecha_imposible_se_salta_y_se_usa_la_siguiente_valida()
    {
        var r = ExtractorCampos.Extraer([L(("31/02/2026", 99)), L(("28/02/2026", 91))]);
        Assert.Equal(new DateTime(2026, 2, 28), r.FechaHora);
        Assert.Equal(0.91m, r.ConfFecha);
    }

    [Fact]
    public void Hora_sin_fecha_no_basta()
    {
        var r = ExtractorCampos.Extraer([L(("Hora:", 99), ("14:22", 99))]);
        Assert.Null(r.FechaHora);
        Assert.Equal(0m, r.ConfFecha);
    }

    [Fact]
    public void Hora_invalida_se_ignora()
    {
        var r = ExtractorCampos.Extraer([L(("10/03/2026", 95)), L(("25:61", 10))]);
        Assert.Equal(new DateTime(2026, 3, 10), r.FechaHora);
        Assert.Equal(0.95m, r.ConfFecha);
    }

    [Theory]
    [InlineData("Operación", "AN12345678")]
    [InlineData("Operacion:", "AN12345678")]
    [InlineData("Cod.operación", "an12345678")]
    public void Codigo_con_sus_etiquetas_se_normaliza_a_mayusculas(string etiqueta, string codigo)
    {
        var r = ExtractorCampos.Extraer([L((etiqueta, 90), (codigo, 92))]);
        Assert.Equal("AN12345678", r.Codigo);
        Assert.Equal(0.92m, r.ConfCodigo);
    }

    [Theory]
    [InlineData("Operación no completada")]  // título de la plantilla
    [InlineData("AN12345678")]               // código sin etiqueta
    [InlineData("Operación AN1234")]         // demasiado corto
    [InlineData("Operación AN12345678901234")] // demasiado largo
    public void Sin_codigo_valido(string texto)
    {
        var r = ExtractorCampos.Extraer([L(texto.Split(' ').Select(t => (t, 99.0)).ToArray())]);
        Assert.Null(r.Codigo);
        Assert.Equal(0m, r.ConfCodigo);
    }

    [Fact]
    public void Codigo_no_se_corrige()
    {
        // El formato del banco no usa O ni I, pero no se corrigen (SPEC §2.1).
        var r = ExtractorCampos.Extraer([L(("Operación", 90), ("ANO2345I78", 80))]);
        Assert.Equal("ANO2345I78", r.Codigo);
    }

    [Theory]
    [InlineData(89.9, 90.0, 0.900)]   // 0.8995 → 0.900: válido tras redondear
    [InlineData(89.9, 89.998, 0.899)] // 0.89949 → 0.899
    public void Confianza_se_redondea_a_tres_decimales(double c1, double c2, double esperado)
    {
        var r = ExtractorCampos.Extraer([L(("S/", c1), ("5.00", c2))]);
        Assert.Equal((decimal)esperado, r.ConfMonto);
    }

    [Fact]
    public void Documento_vacio_deja_todo_nulo()
    {
        var r = ExtractorCampos.Extraer([]);
        Assert.Null(r.Monto);
        Assert.Null(r.Moneda);
        Assert.Null(r.FechaHora);
        Assert.Null(r.Codigo);
        Assert.Equal((0m, 0m, 0m), (r.ConfMonto, r.ConfFecha, r.ConfCodigo));
    }
}
