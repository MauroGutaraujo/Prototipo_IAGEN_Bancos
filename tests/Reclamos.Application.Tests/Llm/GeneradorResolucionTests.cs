using Reclamos.Application.Agentes;
using Reclamos.Application.Llm;
using Reclamos.Application.Rag;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;

namespace Reclamos.Application.Tests.Llm;

public class GeneradorResolucionTests
{
    private static readonly TransaccionCore Tx = new(
        "AN12345678", 1, 1234.5m, Moneda.PEN, new DateTime(2026, 3, 10, 9, 5, 0), "Comercio X",
        EstadoTransaccion.Completada, true, "DEV-1", false);

    private static SolicitudResolucion Solicitud(params FragmentoRecuperado[] fragmentos) =>
        new(123, Ruta.Procedente, "R5", "CARGO_DUPLICADO", Intencion.C1, Tx, fragmentos);

    private static GeneradorResolucion Generador(LlmFalso llm)
    {
        var repo = new RepositorioPrompts(PromptsTests.DirectorioPrompts());
        return new GeneradorResolucion(llm, repo.Cargar("generador.v2"), repo.Cargar("regeneracion.v1"),
            new ParametrosLlm(0, 200, 600), "plazo de prueba P-01");
    }

    [Fact]
    public async Task Los_hechos_van_con_el_formato_que_verifica_el_guardrail()
    {
        var llm = new LlmFalso("borrador");
        var fragmento = new FragmentoRecuperado("sbs-art-7", 0.5, "Norma", "7", "Texto del artículo 7");

        var r = await Generador(llm).GenerarAsync(Solicitud(fragmento), "m1", TestContext.Current.CancellationToken);

        Assert.Equal("borrador", r.Borrador);
        var (modelo, mensajes, maxTokens, json) = llm.Llamadas[0];
        Assert.Equal(("m1", 600, false), (modelo, maxTokens, json));
        var usuario = mensajes[1].Texto;
        Assert.Contains("Número de reclamo: 123", usuario);
        Assert.Contains("S/ 1,234.50", usuario);
        Assert.Contains("10/03/2026 09:05", usuario);
        Assert.Contains("AN12345678", usuario);
        Assert.Contains("Plazo aplicable: plazo de prueba P-01", usuario);
        Assert.Contains("[F:sbs-art-7] Texto del artículo 7", usuario);
        Assert.Contains("PROCEDENTE (regla R5: CARGO_DUPLICADO)", usuario);
        Assert.Contains("DECISIÓN: PROCEDENTE", mensajes[0].Texto);
    }

    [Theory]
    [InlineData(Moneda.USD, "US$ 45.00")]
    [InlineData(Moneda.PEN, "S/ 45.00")]
    public void Formato_de_monto(Moneda moneda, string esperado)
    {
        Assert.Equal(esperado, FormatoHechos.Monto(45m, moneda));
    }

    [Fact]
    public async Task Sin_fragmentos_en_ablacion_la_seccion_queda_vacia()
    {
        var llm = new LlmFalso("b");
        await Generador(llm).GenerarAsync(Solicitud(), "m1", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("[F:", llm.Llamadas[0].Mensajes[1].Texto);
    }

    [Fact]
    public async Task Regenerar_reenvia_la_conversacion_con_el_borrador_y_las_fallas()
    {
        var llm = new LlmFalso("borrador 1", "borrador 2");
        var generador = Generador(llm);
        var primero = await generador.GenerarAsync(Solicitud(), "m1", TestContext.Current.CancellationToken);

        var segundo = await generador.RegenerarAsync(primero, ["Falta el plazo", "Monto distinto"], "m1",
            TestContext.Current.CancellationToken);

        Assert.Equal("borrador 2", segundo.Borrador);
        var mensajes = llm.Llamadas[1].Mensajes;
        Assert.Equal([RolMensaje.Sistema, RolMensaje.Usuario, RolMensaje.Asistente, RolMensaje.Usuario], mensajes.Select(m => m.Rol));
        Assert.Equal("borrador 1", mensajes[2].Texto);
        Assert.Contains("- Falta el plazo", mensajes[3].Texto);
        Assert.Contains("- Monto distinto", mensajes[3].Texto);
    }
}
