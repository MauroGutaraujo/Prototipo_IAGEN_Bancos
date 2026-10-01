using System.Globalization;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R3Tests
{
    [Fact]
    public void Monto_igual_al_umbral_no_deriva()
    {
        Assert.Equal("R6", Decidir(Entrada(Tx(monto: 1000.00m))).Regla);
    }

    [Fact]
    public void Monto_sobre_el_umbral_deriva()
    {
        Assert.Equal(new(Ruta.Derivar, "R3", "MONTO_SUPERA_UMBRAL"), Decidir(Entrada(Tx(monto: 1000.01m))));
    }

    [Theory]
    [InlineData("270.28", "R3")] // 270.28 × 3.7 = 1000.036
    [InlineData("270.27", "R6")] // 270.27 × 3.7 = 999.999
    public void Usd_se_convierte_con_el_tipo_de_cambio_del_dia(string monto, string regla)
    {
        var tx = Tx(monto: decimal.Parse(monto, CultureInfo.InvariantCulture), moneda: Moneda.USD);
        var entrada = Entrada(tx) with { Core = Core(tx, tipoCambio: 3.7m) };
        Assert.Equal(regla, Decidir(entrada).Regla);
    }

    [Fact]
    public void Usd_sin_tipo_de_cambio_deriva()
    {
        var tx = Tx(moneda: Moneda.USD);
        Assert.Equal(new(Ruta.Derivar, "R3", "TIPO_CAMBIO_NO_DISPONIBLE"), Decidir(Entrada(tx)));
    }

    [Fact]
    public void Umbral_es_configurable()
    {
        var opciones = Opciones();
        opciones.UmbralRiesgoPen = 500m;
        Assert.Equal("R3", new AgenteLegal(opciones).Decidir(Entrada(Tx(monto: 600m))).Regla);
    }
}
