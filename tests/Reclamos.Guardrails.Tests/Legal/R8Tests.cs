using Reclamos.Domain.Enums;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R8Tests
{
    [Fact]
    public void Sin_autenticacion_reforzada_deriva()
    {
        var r = Decidir(Entrada(Tx(auth: false), Intencion.C2));
        Assert.Equal(new(Ruta.Derivar, "R8", "SIN_AUTENTICACION_REFORZADA"), r);
    }

    [Fact]
    public void Con_indicador_de_riesgo_deriva()
    {
        var r = Decidir(Entrada(Tx(riesgo: true), Intencion.C2));
        Assert.Equal(new(Ruta.Derivar, "R8", "INDICADOR_RIESGO"), r);
    }

    [Theory]
    [InlineData("DEV-OTRO")]
    [InlineData(null)]
    public void Dispositivo_no_registrado_deriva(string? dispositivo)
    {
        var r = Decidir(Entrada(Tx(dispositivo: dispositivo), Intencion.C2));
        Assert.Equal(new(Ruta.Derivar, "R8", "DISPOSITIVO_NO_REGISTRADO"), r);
    }

    [Fact]
    public void Motivo_lista_todas_las_senales()
    {
        var r = Decidir(Entrada(Tx(auth: false, riesgo: true, dispositivo: null), Intencion.C2));
        Assert.Equal("SIN_AUTENTICACION_REFORZADA,INDICADOR_RIESGO,DISPOSITIVO_NO_REGISTRADO", r.Motivo);
    }
}
