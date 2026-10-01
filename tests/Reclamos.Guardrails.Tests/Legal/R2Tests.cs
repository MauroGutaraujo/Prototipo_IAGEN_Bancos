using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R2Tests
{
    [Fact]
    public void Texto_con_otro_monto_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Monto = 150.51m } });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:texto.monto"), r);
    }

    [Fact]
    public void Texto_con_otra_moneda_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Moneda = Moneda.USD } });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:texto.monto"), r);
    }

    [Fact]
    public void Texto_con_monto_sin_moneda_compara_solo_el_monto()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Moneda = null } });
        Assert.Equal("R6", r.Regla);
    }

    [Fact]
    public void Texto_con_otra_fecha_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Fecha = new DateOnly(2026, 3, 9) } });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:texto.fecha"), r);
    }

    [Fact]
    public void Texto_con_otro_codigo_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Codigo = "AN12345679" } });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:texto.codigo"), r);
    }

    [Fact]
    public void Codigo_se_normaliza_antes_de_comparar()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with
        {
            Clasificador = Cls(tx) with { Codigo = " an1234 5678 " },
            Ocr = Ocr(tx) with { Codigo = "an12345678" },
        });
        Assert.Equal("R6", r.Regla);
    }

    [Fact]
    public void Datos_ausentes_en_el_texto_no_se_comparan()
    {
        var tx = Tx();
        var cls = Cls(tx) with { Monto = null, Moneda = null, Fecha = null, Codigo = null };
        Assert.Equal("R6", Decidir(Entrada(tx) with { Clasificador = cls }).Regla);
    }

    [Fact]
    public void Ocr_con_otro_monto_moneda_fecha_y_codigo_deriva_y_lista_todo()
    {
        var tx = Tx();
        var ocr = Ocr(tx) with { Monto = 105.50m, FechaHora = Fecha.AddDays(-1), Codigo = "AN00000000" };
        var r = Decidir(Entrada(tx) with { Ocr = ocr });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:ocr.monto,ocr.fecha,ocr.codigo"), r);
    }

    [Fact]
    public void Ocr_con_otra_moneda_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Ocr = Ocr(tx) with { Moneda = Moneda.USD } });
        Assert.Equal(new(Ruta.Derivar, "R2", "DISCREPANCIA:ocr.monto"), r);
    }

    [Fact]
    public void Fecha_se_compara_a_nivel_de_dia()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Ocr = Ocr(tx) with { FechaHora = Fecha.AddHours(5) } });
        Assert.Equal("R6", r.Regla);
    }

    [Fact]
    public void Datos_de_un_clasificador_invalido_no_se_comparan()
    {
        var tx = Tx();
        var cls = new SalidaClasificador(false, null, 999m, Moneda.USD, new DateOnly(2020, 1, 1), "X");
        Assert.Equal("R4", Decidir(Entrada(tx) with { Clasificador = cls }).Regla);
    }
}
