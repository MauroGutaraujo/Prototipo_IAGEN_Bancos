using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

public class R5Tests
{
    private static EntradaLegal ConDuplicado(TimeSpan delta)
    {
        var tx = Tx();
        var duplicado = tx with { Codigo = "AN00000001", FechaHora = tx.FechaHora + delta };
        return Entrada(tx) with { Core = Core(tx, null, duplicado) };
    }

    [Fact]
    public void Duplicado_minutos_antes_es_procedente()
    {
        Assert.Equal(new(Ruta.Procedente, "R5", "CARGO_DUPLICADO"), Decidir(ConDuplicado(TimeSpan.FromMinutes(-7))));
    }

    [Fact]
    public void Duplicado_posterior_tambien_cuenta()
    {
        Assert.Equal("R5", Decidir(ConDuplicado(TimeSpan.FromMinutes(30))).Regla);
    }

    [Fact]
    public void Diferencia_de_24_horas_exactas_es_duplicado()
    {
        Assert.Equal("R5", Decidir(ConDuplicado(TimeSpan.FromHours(-24))).Regla);
    }

    [Fact]
    public void Ventana_es_configurable()
    {
        Assert.Equal("R5", Decidir(ConDuplicado(TimeSpan.FromHours(-30)), ventanaHoras: 48).Regla);
    }
}
