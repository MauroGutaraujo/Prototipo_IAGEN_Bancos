using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R1Tests
{
    [Fact]
    public void Confianza_igual_al_umbral_es_valida()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Ocr = Ocr(tx, conf: 0.90m) });
        Assert.NotEqual("R1", r.Regla);
    }

    [Theory]
    [InlineData("monto")]
    [InlineData("fecha")]
    [InlineData("codigo")]
    public void Confianza_bajo_el_umbral_deriva(string campo)
    {
        var tx = Tx();
        var ocr = Ocr(tx) with
        {
            ConfMonto = campo == "monto" ? 0.899m : 0.95m,
            ConfFecha = campo == "fecha" ? 0.899m : 0.95m,
            ConfCodigo = campo == "codigo" ? 0.899m : 0.95m,
        };
        var r = Decidir(Entrada(tx) with { Ocr = ocr });
        Assert.Equal(new(Ruta.Derivar, "R1", $"OCR_ILEGIBLE:{campo}"), r);
    }

    [Fact]
    public void Valores_sin_formato_valido_derivan_y_se_listan_en_el_motivo()
    {
        var tx = Tx();
        var ocr = Ocr(tx) with { Monto = null, FechaHora = null, Codigo = " " };
        var r = Decidir(Entrada(tx) with { Ocr = ocr });
        Assert.Equal(new(Ruta.Derivar, "R1", "OCR_ILEGIBLE:monto,fecha,codigo"), r);
    }

    [Fact]
    public void Monto_sin_moneda_reconocida_es_invalido()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Ocr = Ocr(tx) with { Moneda = null } });
        Assert.Equal(new(Ruta.Derivar, "R1", "OCR_ILEGIBLE:monto"), r);
    }
}
