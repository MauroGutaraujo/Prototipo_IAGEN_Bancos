using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Salida;
using static Reclamos.Guardrails.Tests.Salida.Borradores;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>§2.6.3: el marcador DECISIÓN coincide con la ruta del Agente Legal.</summary>
public class SentidoTests
{
    [Fact]
    public void Sin_marcador_falla()
    {
        var v = Verificar(Valido.Replace("DECISIÓN: PROCEDENTE", "Su reclamo procede."));
        Assert.Equal([TipoFalla.SinMarcadorDecision], Tipos(v));
    }

    [Fact]
    public void Marcador_contrario_a_la_ruta_falla()
    {
        var v = Verificar(Valido, ruta: Ruta.Improcedente);
        Assert.Equal(new FallaGuardrail(TipoFalla.SentidoContradictorio, "PROCEDENTE"), Assert.Single(v.Fallas));
    }

    [Fact]
    public void Dos_marcadores_contradictorios_fallan()
    {
        var v = Verificar(Valido.Replace("DECISIÓN: PROCEDENTE", "DECISIÓN: PROCEDENTE\nDECISIÓN: IMPROCEDENTE"));
        Assert.Equal([TipoFalla.SentidoContradictorio], Tipos(v));
    }

    [Theory]
    [InlineData("DECISIÓN: IMPROCEDENTE")]
    [InlineData("  Decisión:  improcedente  ")]
    [InlineData("DECISION: IMPROCEDENTE")]
    public void Marcador_improcedente_coincide_con_ruta_improcedente(string marcador)
    {
        var v = Verificar(Valido.Replace("DECISIÓN: PROCEDENTE", marcador), ruta: Ruta.Improcedente);
        Assert.True(v.Aprobado);
    }

    [Fact]
    public void Ruta_derivar_no_llega_a_redaccion()
    {
        Assert.Throws<ArgumentException>(() => Verificar(Valido, ruta: Ruta.Derivar));
    }
}
