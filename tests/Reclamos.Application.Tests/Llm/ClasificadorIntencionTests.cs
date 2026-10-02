using Reclamos.Application.Agentes;
using Reclamos.Application.Llm;
using Reclamos.Domain.Enums;

namespace Reclamos.Application.Tests.Llm;

public class ClasificadorIntencionTests
{
    private const string Valida =
        """{"intencion":"C1","monto":150.50,"moneda":"PEN","fecha":"2026-03-10","codigo":"AN12345678"}""";

    private static ClasificadorIntencion Clasificador(LlmFalso llm)
    {
        var repo = new RepositorioPrompts(PromptsTests.DirectorioPrompts());
        return new ClasificadorIntencion(llm, repo.Cargar("clasificador.v1"), repo.LeerTexto("clasificador.schema.json"),
            new ParametrosLlm(0, 200, 600));
    }

    [Fact]
    public async Task Salida_valida_se_mapea_al_contrato_del_agente_legal()
    {
        var llm = new LlmFalso(Valida);

        var r = await Clasificador(llm).ClasificarAsync("me cobraron dos veces", "m1", TestContext.Current.CancellationToken);

        Assert.True(r.Salida.EsValida);
        Assert.Equal(Intencion.C1, r.Salida.Intencion);
        Assert.Equal(150.50m, r.Salida.Monto);
        Assert.Equal(Moneda.PEN, r.Salida.Moneda);
        Assert.Equal(new DateOnly(2026, 3, 10), r.Salida.Fecha);
        Assert.Equal("AN12345678", r.Salida.Codigo);
        Assert.Equal(1, r.Intentos);
        Assert.Equal(("modelo-version-001", 10, 5), (r.ModeloVersion, r.TokensIn, r.TokensOut));
    }

    [Fact]
    public async Task La_llamada_usa_el_prompt_versionado_json_y_max_tokens_de_clasificacion()
    {
        var llm = new LlmFalso(Valida);

        await Clasificador(llm).ClasificarAsync("narración {{x}} del cliente", "m1", TestContext.Current.CancellationToken);

        var (modelo, mensajes, maxTokens, json) = llm.Llamadas[0];
        Assert.Equal(("m1", 200, true), (modelo, maxTokens, json));
        Assert.Equal([RolMensaje.Sistema, RolMensaje.Usuario], mensajes.Select(m => m.Rol));
        Assert.Contains("narración {{x}} del cliente", mensajes[1].Texto);
    }

    [Fact]
    public async Task Fuera_de_catalogo_y_nulos_son_validos()
    {
        var llm = new LlmFalso("""{"intencion":"FUERA_DE_CATALOGO","monto":null,"moneda":null,"fecha":null,"codigo":null}""");

        var r = await Clasificador(llm).ClasificarAsync("hola", "m1", TestContext.Current.CancellationToken);

        Assert.True(r.Salida.EsValida);
        Assert.Equal(Intencion.FueraDeCatalogo, r.Salida.Intencion);
        Assert.Null(r.Salida.Monto);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("""{"intencion":"C9","monto":null,"moneda":null,"fecha":null,"codigo":null}""")]   // fuera del enum
    [InlineData("""{"intencion":"C1","monto":null,"moneda":null,"fecha":null}""")]                 // falta campo
    [InlineData("""{"intencion":"C1","monto":-5,"moneda":null,"fecha":null,"codigo":null}""")]     // monto negativo
    [InlineData("""{"intencion":"C1","monto":null,"moneda":null,"fecha":"2026-02-31","codigo":null}""")] // fecha imposible
    [InlineData("""{"intencion":"C1","monto":null,"moneda":null,"fecha":null,"codigo":null,"x":1}""")]   // propiedad extra
    public async Task Salida_invalida_se_reintenta_una_vez(string invalida)
    {
        var llm = new LlmFalso(invalida, Valida);

        var r = await Clasificador(llm).ClasificarAsync("hola", "m1", TestContext.Current.CancellationToken);

        Assert.True(r.Salida.EsValida);
        Assert.Equal(2, r.Intentos);
        Assert.Equal((20, 10), (r.TokensIn, r.TokensOut)); // se suman ambos intentos
    }

    [Fact]
    public async Task Dos_salidas_invalidas_quedan_como_invalidas_para_R4()
    {
        var llm = new LlmFalso("x", "```json\n{}\n```");

        var r = await Clasificador(llm).ClasificarAsync("hola", "m1", TestContext.Current.CancellationToken);

        Assert.False(r.Salida.EsValida);
        Assert.Null(r.Salida.Intencion);
        Assert.Equal(2, r.Intentos);
        Assert.Equal(2, llm.Llamadas.Count);
    }
}
