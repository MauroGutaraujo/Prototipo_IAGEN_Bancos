using static Reclamos.Guardrails.Tests.Salida.Borradores;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>T2: el guardrail evalúa y registra, pero no bloquea.</summary>
public class AblacionTests
{
    private static readonly string BorradorConFallas = Valido.Replace("S/ 150.50", "S/ 999.00");

    [Fact]
    public void En_T1_un_borrador_con_fallas_bloquea()
    {
        var v = Verificar(BorradorConFallas);
        Assert.False(v.Aprobado);
        Assert.True(v.Bloquea);
    }

    [Fact]
    public void En_T2_registra_las_fallas_pero_no_bloquea()
    {
        var v = Verificar(BorradorConFallas, admitidos: [], ablacion: true);
        Assert.False(v.Aprobado);
        Assert.False(v.Bloquea);
        Assert.Equal(2, v.AfirmacionesNoSustentadas); // monto y cita
    }

    [Fact]
    public void Borrador_aprobado_no_bloquea()
    {
        Assert.False(Verificar(Valido).Bloquea);
    }
}
