using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R6Tests
{
    [Fact]
    public void Sin_otros_cargos_es_improcedente()
    {
        Assert.Equal(new(Ruta.Improcedente, "R6", "SIN_CARGO_DUPLICADO"), Decidir(Entrada(Tx())));
    }

    [Fact]
    public void Cargos_parecidos_que_no_son_duplicados_no_cuentan()
    {
        var tx = Tx();
        var fueraDeVentana = tx with { Codigo = "AN00000001", FechaHora = tx.FechaHora.AddHours(-24).AddMinutes(-1) };
        var otroMonto = tx with { Codigo = "AN00000002", Monto = 151.50m };
        var otraMoneda = tx with { Codigo = "AN00000003", Moneda = Moneda.USD };
        var otroComercio = tx with { Codigo = "AN00000004", Comercio = "Comercio Y" };
        var otroCliente = tx with { Codigo = "AN00000005", IdCliente = 2 };
        var entrada = Entrada(tx) with
        {
            Core = Core(tx, null, fueraDeVentana, otroMonto, otraMoneda, otroComercio, otroCliente),
        };
        Assert.Equal("R6", Decidir(entrada).Regla);
    }
}
