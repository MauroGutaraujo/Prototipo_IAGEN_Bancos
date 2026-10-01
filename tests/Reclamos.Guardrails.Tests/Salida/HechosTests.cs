using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Salida;
using static Reclamos.Guardrails.Tests.Salida.Borradores;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>§2.6.2: todo monto, fecha y código mencionado coincide con los hechos verificados.</summary>
public class HechosTests
{
    [Theory]
    [InlineData("S/ 150.00")]
    [InlineData("US$ 150.50")]
    [InlineData("S/ 150")]
    public void Monto_distinto_falla(string monto)
    {
        var v = Verificar(Valido.Replace("S/ 150.50", monto));
        Assert.Equal(new FallaGuardrail(TipoFalla.HechoNoCoincide, $"monto:{monto}"), Assert.Single(v.Fallas));
        Assert.Equal(1, v.AfirmacionesNoSustentadas);
    }

    [Theory]
    [InlineData("S/150.50")]
    [InlineData("S/ 1,150.50", 1150.50)]
    [InlineData("US$ 45.00", 45.00, Moneda.USD)]
    public void Formatos_de_monto_reconocidos(string monto, double valor = 150.50, Moneda moneda = Moneda.PEN)
    {
        var hechos = Hechos with { Monto = (decimal)valor, Moneda = moneda };
        Assert.True(Verificar(Valido.Replace("S/ 150.50", monto), hechos: hechos).Aprobado);
    }

    [Theory]
    [InlineData("10-03-2026")]
    [InlineData("10 de marzo de 2026")]
    [InlineData("10 de Marzo del 2026")]
    public void Formatos_de_fecha_reconocidos(string fecha)
    {
        var v = Verificar(Valido.Replace("10/03/2026", fecha));
        Assert.True(v.Aprobado);
        Assert.Equal(4, v.AfirmacionesVerificables);
    }

    [Fact]
    public void Mes_setiembre_y_septiembre_son_equivalentes()
    {
        var hechos = Hechos with { FechaHora = new DateTime(2026, 9, 5) };
        Assert.True(Verificar(Valido.Replace("10/03/2026", "5 de setiembre de 2026"), hechos: hechos).Aprobado);
        Assert.True(Verificar(Valido.Replace("10/03/2026", "5 de septiembre de 2026"), hechos: hechos).Aprobado);
    }

    [Theory]
    [InlineData("11/03/2026")]
    [InlineData("11 de marzo de 2026")]
    [InlineData("31/02/2026")]
    [InlineData("10/13/2026")]
    [InlineData("10/00/2026")]
    [InlineData("00/03/2026")]
    public void Fecha_distinta_o_imposible_falla(string fecha)
    {
        var v = Verificar(Valido.Replace("10/03/2026", fecha));
        Assert.Equal(new FallaGuardrail(TipoFalla.HechoNoCoincide, $"fecha:{fecha}"), Assert.Single(v.Fallas));
    }

    [Fact]
    public void Codigo_distinto_falla()
    {
        var v = Verificar(Valido.Replace("AN12345678", "AN12345679"));
        Assert.Equal(new FallaGuardrail(TipoFalla.HechoNoCoincide, "codigo:AN12345679"), Assert.Single(v.Fallas));
    }

    [Fact]
    public void Hechos_repetidos_cuentan_cada_mencion()
    {
        var v = Verificar(Valido + "\nReiteramos: S/ 150.50 y S/ 99.00.");
        Assert.Equal(6, v.AfirmacionesVerificables);
        Assert.Equal(1, v.AfirmacionesNoSustentadas);
    }
}
