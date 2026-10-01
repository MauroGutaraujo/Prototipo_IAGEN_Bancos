using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R7Tests
{
    [Theory]
    [InlineData(EstadoTransaccion.Fallida)]
    [InlineData(EstadoTransaccion.NoCompletada)]
    public void Operacion_no_completada_con_cargo_es_procedente(EstadoTransaccion estado)
    {
        var r = Decidir(Entrada(Tx(estado: estado), Intencion.C3));
        Assert.Equal(new(Ruta.Procedente, "R7", "OPERACION_NO_COMPLETADA"), r);
    }

    [Fact]
    public void Operacion_completada_se_deriva_para_conciliacion()
    {
        var r = Decidir(Entrada(Tx(estado: EstadoTransaccion.Completada), Intencion.C3));
        Assert.Equal(new(Ruta.Derivar, "R7", "CONCILIACION"), r);
    }

    [Fact]
    public void Operacion_fallida_sin_cargo_se_deriva_para_conciliacion()
    {
        var r = Decidir(Entrada(Tx(monto: 0m, estado: EstadoTransaccion.Fallida), Intencion.C3));
        Assert.Equal(new(Ruta.Derivar, "R7", "CONCILIACION"), r);
    }
}
