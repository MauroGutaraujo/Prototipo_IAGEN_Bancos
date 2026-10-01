using Reclamos.Guardrails.Salida;
using static Reclamos.Guardrails.Tests.Salida.Borradores;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>§2.6.4: número de reclamo, instancia y plazo.</summary>
public class ContenidosMinimosTests
{
    [Fact]
    public void Falta_numero_de_reclamo()
    {
        var v = Verificar(Valido.Replace("N.° 123", "presentado"));
        Assert.Equal([TipoFalla.FaltaNumeroReclamo], Tipos(v));
    }

    [Fact]
    public void Numero_que_solo_aparece_dentro_de_otro_hecho_no_cuenta()
    {
        // El 150 del monto no es el número de reclamo.
        var v = Verificar(Valido.Replace("N.° 123", "presentado"), hechos: Hechos with { NumeroReclamo = 150 });
        Assert.Equal([TipoFalla.FaltaNumeroReclamo], Tipos(v));
    }

    [Fact]
    public void Falta_instancia()
    {
        var sinInstancias = Valido.Replace(
            "Si no se encuentra conforme, puede acudir a la Defensoría del Cliente Financiero, a la SBS o a Indecopi.", "");
        Assert.Equal([TipoFalla.FaltaInstancia], Tipos(Verificar(sinInstancias)));
    }

    [Fact]
    public void Id_de_cita_no_cuenta_como_instancia()
    {
        var soloCita = Valido.Replace(
            "Si no se encuentra conforme, puede acudir a la Defensoría del Cliente Financiero, a la SBS o a Indecopi.",
            "Ver [F:indecopi-1].");
        var v = Verificar(soloCita, admitidos: [.. Admitidos, "indecopi-1"]);
        Assert.Equal([TipoFalla.FaltaInstancia], Tipos(v));
    }

    [Theory]
    [InlineData("la Defensoria del Cliente Financiero")]
    [InlineData("la SBS")]
    [InlineData("INDECOPI")]
    public void Basta_una_instancia(string instancia)
    {
        var borrador = Valido.Replace(
            "la Defensoría del Cliente Financiero, a la SBS o a Indecopi", instancia);
        Assert.True(Verificar(borrador).Aprobado);
    }

    [Fact]
    public void Falta_plazo()
    {
        var v = Verificar(Valido.Replace("dentro del plazo de prueba P-01", "pronto"));
        Assert.Equal([TipoFalla.FaltaPlazo], Tipos(v));
    }

    [Fact]
    public void Plazo_se_compara_sin_mayusculas_tildes_ni_espacios_extra()
    {
        var borrador = Valido.Replace("dentro del plazo de prueba P-01", "dentro del PERIODO   de prueba p-01");
        Assert.True(Verificar(borrador, plazo: "período de prueba P-01").Aprobado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_plazo_configurado_no_se_puede_construir(string plazo)
    {
        Assert.Throws<ArgumentException>(() => new GuardrailSalida(Opciones(plazo)));
    }

    [Fact]
    public void Argumentos_nulos_se_rechazan()
    {
        Assert.Throws<ArgumentNullException>(() => new GuardrailSalida(null!));
        Assert.Throws<ArgumentNullException>(() => new GuardrailSalida(Opciones()).Verificar(null!));
    }
}
