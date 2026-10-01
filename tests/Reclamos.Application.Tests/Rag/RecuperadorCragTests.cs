using System.Text.Json;
using Reclamos.Application.Rag;
using Reclamos.Domain.Enums;

namespace Reclamos.Application.Tests.Rag;

public class RecuperadorCragTests
{
    /// <summary>Devuelve, en orden, una lista de scores por cada llamada y registra las consultas.</summary>
    private sealed class BuscadorFalso(params double[][] respuestas) : IBuscadorVectorial
    {
        public List<(string Consulta, int TopK)> Llamadas { get; } = [];

        public Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(string consulta, int topK, CancellationToken ct)
        {
            var scores = respuestas[Llamadas.Count];
            Llamadas.Add((consulta, topK));
            IReadOnlyList<FragmentoRecuperado> r = scores
                .Select((s, i) => new FragmentoRecuperado($"f{Llamadas.Count}-{i}", s, "Norma", $"{i}", $"texto {i}"))
                .ToList();
            return Task.FromResult(r);
        }
    }

    private static readonly ConsultaNormativa C1Procedente = new(Intencion.C1, Ruta.Procedente, "R5");

    private static RecuperadorCrag Recuperador(BuscadorFalso buscador, bool habilitado = true) =>
        new(new RagOptions { Enabled = habilitado, Theta = 0.78, TopK = 5 }, buscador);

    [Fact]
    public async Task Fragmento_sobre_theta_en_la_primera_consulta_es_correcta()
    {
        var buscador = new BuscadorFalso([0.91, 0.78, 0.7799]);

        var r = await Recuperador(buscador).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);

        Assert.Equal(CalificacionRecuperacion.Correcta, r.Calificacion);
        Assert.Equal(["f1-0", "f1-1"], r.Admitidos.Select(f => f.Id)); // 0.78 exacto se admite
        Assert.Single(buscador.Llamadas);
        Assert.Equal(5, buscador.Llamadas[0].TopK);
        Assert.False(r.Derivar);
        Assert.Equal("consulta.v1", r.VersionConsulta);
    }

    [Fact]
    public async Task Solo_tras_reformular_es_ambigua_y_la_reformulacion_agrega_normas_y_terminos()
    {
        var buscador = new BuscadorFalso([0.6, 0.5], [0.82, 0.4]);

        var r = await Recuperador(buscador).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);

        Assert.Equal(CalificacionRecuperacion.Ambigua, r.Calificacion);
        Assert.Equal(["f2-0"], r.Admitidos.Select(f => f.Id));
        Assert.Equal(2, buscador.Llamadas.Count);
        var reformulada = buscador.Llamadas[1].Consulta;
        Assert.StartsWith(buscador.Llamadas[0].Consulta, reformulada);
        Assert.Contains("04036-2022", reformulada);
        Assert.Contains("29571", reformulada);
        Assert.Contains("cobro duplicado", reformulada);
        Assert.False(r.Derivar);
    }

    [Fact]
    public async Task Sin_fragmentos_tras_reformular_es_incorrecta_y_deriva()
    {
        var buscador = new BuscadorFalso([0.5], [0.77]);

        var r = await Recuperador(buscador).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);

        Assert.Equal(CalificacionRecuperacion.Incorrecta, r.Calificacion);
        Assert.Empty(r.Admitidos);
        Assert.True(r.Derivar);
        Assert.Equal("CRAG_INCORRECTA", r.MotivoDerivacion);
    }

    [Fact]
    public async Task Ablacion_no_consulta_y_queda_omitida()
    {
        var buscador = new BuscadorFalso();

        var r = await Recuperador(buscador, habilitado: false).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);

        Assert.Equal(CalificacionRecuperacion.Omitida, r.Calificacion);
        Assert.Empty(buscador.Llamadas);
        Assert.Empty(r.Admitidos);
        Assert.False(r.Derivar);
        Assert.Null(r.MotivoDerivacion);
        Assert.Equal("[]", r.FragmentosJson);
    }

    [Fact]
    public async Task FragmentosJson_registra_cada_consulta_con_score_y_admision()
    {
        var buscador = new BuscadorFalso([0.5], [0.9]);

        var r = await Recuperador(buscador).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);

        var json = JsonDocument.Parse(r.FragmentosJson).RootElement;
        Assert.Equal(2, json.GetArrayLength());
        Assert.Equal("f1-0", json[0].GetProperty("id").GetString());
        Assert.Equal(1, json[0].GetProperty("consulta").GetInt32());
        Assert.False(json[0].GetProperty("admitido").GetBoolean());
        Assert.Equal(0.9, json[1].GetProperty("score").GetDouble());
        Assert.True(json[1].GetProperty("admitido").GetBoolean());
    }

    [Theory]
    [InlineData(Intencion.C1, Ruta.Improcedente, "R6", "cobro duplicado")]
    [InlineData(Intencion.C2, Ruta.Improcedente, "R9", "operación no reconocida")]
    [InlineData(Intencion.C3, Ruta.Procedente, "R7", "pasarela")]
    public void La_consulta_depende_solo_de_intencion_ruta_y_regla(Intencion intencion, Ruta ruta, string regla, string termino)
    {
        var c = new ConsultaNormativa(intencion, ruta, regla);
        var consulta = PlantillasConsulta.Consulta(c);
        var reformulada = PlantillasConsulta.Reformular(c);

        Assert.Contains(ruta == Ruta.Procedente ? " procedente" : "improcedente", consulta);
        Assert.Contains(termino, reformulada);
        Assert.Equal(consulta, PlantillasConsulta.Consulta(c with { }));
    }

    [Fact]
    public async Task Un_expediente_derivado_no_llega_a_recuperacion()
    {
        var r = Recuperador(new BuscadorFalso());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            r.RecuperarAsync(new ConsultaNormativa(Intencion.C1, Ruta.Derivar, "R3"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Mide_el_tiempo_de_recuperacion()
    {
        var r = await Recuperador(new BuscadorFalso([0.9])).RecuperarAsync(C1Procedente, TestContext.Current.CancellationToken);
        Assert.True(r.Duracion >= TimeSpan.Zero);
    }

    [Fact]
    public void Namespace_por_version_del_corpus()
    {
        Assert.Equal("corpus-ddf600b6cf2f", RagOptions.Namespace("ddf600b6cf2f920b6f5e49e8bee1f645118de5ad2ca7c626705185d68029499d"));
    }
}
