using Reclamos.Guardrails.Salida;
using static Reclamos.Guardrails.Tests.Salida.Borradores;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>§2.6.1: toda cita [F:id] pertenece a los fragmentos admitidos.</summary>
public class CitasTests
{
    [Fact]
    public void Borrador_valido_se_aprueba_y_cuenta_sus_afirmaciones()
    {
        var v = Verificar(Valido);
        Assert.True(v.Aprobado);
        Assert.Empty(v.Fallas);
        // 1 cita + 1 monto + 1 fecha + 1 código
        Assert.Equal(4, v.AfirmacionesVerificables);
        Assert.Equal(0, v.AfirmacionesNoSustentadas);
    }

    [Fact]
    public void Cita_no_admitida_falla_y_cuenta_como_no_sustentada()
    {
        var v = Verificar(Valido.Replace("[F:sbs-art-10]", "[F:sbs-art-10] y [F:inventada-1]"));
        var falla = Assert.Single(v.Fallas);
        Assert.Equal(new FallaGuardrail(TipoFalla.CitaNoAdmitida, "inventada-1"), falla);
        Assert.Equal(5, v.AfirmacionesVerificables);
        Assert.Equal(1, v.AfirmacionesNoSustentadas);
    }

    [Fact]
    public void Sin_fragmentos_admitidos_cualquier_cita_falla()
    {
        var v = Verificar(Valido, admitidos: []);
        Assert.Equal([TipoFalla.CitaNoAdmitida], Tipos(v));
    }
}
