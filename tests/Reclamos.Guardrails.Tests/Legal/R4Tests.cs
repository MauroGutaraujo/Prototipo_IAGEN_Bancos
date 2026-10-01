using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R4Tests
{
    [Fact]
    public void Clasificador_invalido_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { EsValida = false } });
        Assert.Equal(new(Ruta.Derivar, "R4", "CLASIFICADOR_INVALIDO"), r);
    }

    [Fact]
    public void Clasificador_valido_sin_intencion_deriva()
    {
        var tx = Tx();
        var r = Decidir(Entrada(tx) with { Clasificador = Cls(tx) with { Intencion = null } });
        Assert.Equal(new(Ruta.Derivar, "R4", "CLASIFICADOR_INVALIDO"), r);
    }

    [Fact]
    public void Fuera_de_catalogo_deriva()
    {
        Assert.Equal(new(Ruta.Derivar, "R4", "FUERA_DE_CATALOGO"), Decidir(Entrada(Tx(), Intencion.FueraDeCatalogo)));
    }
}
