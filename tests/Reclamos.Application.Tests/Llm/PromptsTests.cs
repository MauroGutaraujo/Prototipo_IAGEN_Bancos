using Reclamos.Application.Llm;

namespace Reclamos.Application.Tests.Llm;

public class PromptsTests
{
    internal static string DirectorioPrompts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Reclamos.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "prompts");
    }

    [Fact]
    public void Renderiza_variables_y_secciones_sin_reinterpretar_valores()
    {
        const string plantilla = "Reclamo {{id}}:\n{{#fragmentos}}\n[F:{{id}}] {{texto}}\n{{/fragmentos}}\nFin {{narracion}}";
        IReadOnlyList<IReadOnlyDictionary<string, string>> fragmentos =
        [
            new Dictionary<string, string> { ["id"] = "a", ["texto"] = "uno {{narracion}}" },
            new Dictionary<string, string> { ["id"] = "b", ["texto"] = "dos" },
        ];

        var r = Renderizador.Renderizar(plantilla,
            new Dictionary<string, string> { ["id"] = "7", ["narracion"] = "pagué {{id}}" },
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> { ["fragmentos"] = fragmentos });

        Assert.Equal("Reclamo 7:\n[F:a] uno {{narracion}}\n[F:b] dos\nFin pagué {{id}}", r);
    }

    [Fact]
    public void Lista_vacia_elimina_la_seccion()
    {
        var r = Renderizador.Renderizar("A\n{{#xs}}\n- {{v}}\n{{/xs}}\nB", new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> { ["xs"] = [] });
        Assert.Equal("A\nB", r);
    }

    [Fact]
    public void Variable_o_lista_ausente_es_un_error()
    {
        Assert.Throws<KeyNotFoundException>(() => Renderizador.Renderizar("{{x}}", new Dictionary<string, string>()));
        Assert.Throws<KeyNotFoundException>(() => Renderizador.Renderizar("{{#xs}}\n{{/xs}}\n", new Dictionary<string, string>()));
    }

    [Fact]
    public void Separa_sistema_y_usuario_e_ignora_comentarios()
    {
        var p = PlantillaPrompt.Parsear("x.v1", "# SISTEMA — título\n<!-- nota -->\nReglas\n\n# USUARIO\nHola {{n}}\r\n");
        Assert.Equal(("x.v1", "Reglas", "Hola {{n}}"), (p.Version, p.Sistema, p.Usuario));
    }

    [Fact]
    public void Prompt_sin_usuario_es_invalido()
    {
        Assert.Throws<FormatException>(() => PlantillaPrompt.Parsear("x", "# SISTEMA\nsolo sistema"));
    }

    [Theory]
    [InlineData("clasificador.v1", true)]
    [InlineData("generador.v2", true)]
    [InlineData("regeneracion.v1", false)]
    public void Los_prompts_versionados_del_repo_se_cargan(string version, bool tieneSistema)
    {
        var p = new RepositorioPrompts(DirectorioPrompts()).Cargar(version);
        Assert.Equal(tieneSistema, p.Sistema is not null);
        Assert.False(string.IsNullOrWhiteSpace(p.Usuario));
    }

    [Fact]
    public void Registro_de_modelos_desde_json()
    {
        var r = RegistroModelos.Desde("""
            { "modelos": [ { "id": "m1", "proveedor": "P", "segmento": "s", "endpoint": "https://x/v1/", "apiKeyEnv": "K", "modelName": "n" } ],
              "parametros": { "temperature": 0, "maxTokensClasificacion": 200, "maxTokensGeneracion": 600 } }
            """);
        Assert.Equal("n", r.Buscar("m1")!.ModelName);
        Assert.Null(r.Buscar("otro"));
        Assert.Equal(new ParametrosLlm(0, 200, 600), r.Parametros);
    }
}
