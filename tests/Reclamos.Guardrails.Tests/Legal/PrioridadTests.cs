using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using static Reclamos.Guardrails.Tests.Legal.Datos;

namespace Reclamos.Guardrails.Tests.Legal;

/// <summary>R1 &gt; R2 &gt; R3 &gt; R4 &gt; reglas por tipología.</summary>
public class PrioridadTests
{
    // Entrada que dispararía R1, R2, R3 y R4 a la vez.
    private static EntradaLegal Todas()
    {
        var tx = Tx(monto: 5000m);
        return new EntradaLegal(
            Ocr(tx) with { ConfCodigo = 0.5m },
            Cls(tx, Intencion.FueraDeCatalogo) with { Monto = 1m },
            Core(tx));
    }

    [Fact]
    public void R1_antes_que_R2_R3_R4()
    {
        Assert.Equal("R1", Decidir(Todas()).Regla);
    }

    [Fact]
    public void R2_antes_que_R3_R4()
    {
        var e = Todas();
        Assert.Equal("R2", Decidir(e with { Ocr = e.Ocr with { ConfCodigo = 0.95m } }).Regla);
    }

    [Fact]
    public void R3_antes_que_R4()
    {
        var e = Todas();
        var sinDiscrepancia = e with
        {
            Ocr = e.Ocr with { ConfCodigo = 0.95m },
            Clasificador = e.Clasificador with { Monto = null },
        };
        Assert.Equal("R3", Decidir(sinDiscrepancia).Regla);
    }

    [Fact]
    public void R4_antes_que_las_reglas_por_tipologia()
    {
        Assert.Equal("R4", Decidir(Entrada(Tx(auth: false), Intencion.FueraDeCatalogo)).Regla);
    }

    [Fact]
    public void Argumentos_nulos_se_rechazan()
    {
        Assert.Throws<ArgumentNullException>(() => new AgenteLegal(null!));
        Assert.Throws<ArgumentNullException>(() => new AgenteLegal(Opciones()).Decidir(null!));
    }
}
