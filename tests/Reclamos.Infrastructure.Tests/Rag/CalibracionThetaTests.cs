using System.Globalization;
using System.Text;
using System.Text.Json;
using Reclamos.Application.Rag;
using Reclamos.Infrastructure.Rag;

namespace Reclamos.Infrastructure.Tests.Rag;

/// <summary>
/// Revisión de θ (SPEC §2.4) con consultas de DESARROLLO (knowledge/consultas_desarrollo.jsonl, clasificación
/// validada por el estudiante). Obtiene el score de todos los fragmentos y REPORTA cómo separa θ los
/// pertinentes de los no pertinentes. No elige ni cambia θ: esa decisión es del estudiante.
/// </summary>
[Trait("Categoria", "CalibracionRag")]
public class CalibracionThetaTests
{
    private sealed record ConsultaDesarrollo(string Id, string Consulta, string[] Pertinentes, string Tema);

    [Fact]
    public async Task Distribucion_de_scores_de_las_consultas_de_desarrollo()
    {
        var claves = new ClavesRag(Environment.GetEnvironmentVariable("PINECONE_API_KEY"));
        Assert.SkipWhen(string.IsNullOrWhiteSpace(claves.Pinecone), "Falta PINECONE_API_KEY");

        var ct = TestContext.Current.CancellationToken;
        var opciones = new RagOptions();
        if (Environment.GetEnvironmentVariable("PINECONE_INDEX") is { Length: > 0 } indice)
            opciones.Index = indice;

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var sha = JsonDocument.Parse(await File.ReadAllTextAsync(CorpusNormativo.ResolverRuta("knowledge/manifest.json"), ct))
            .RootElement.GetProperty("fragmentos_sha256").GetString()!;
        var corpus = CorpusNormativo.Cargar(opciones.CorpusPath, sha);
        var consultas = (await File.ReadAllLinesAsync(CorpusNormativo.ResolverRuta("knowledge/consultas_desarrollo.jsonl"), ct))
            .Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Deserialize<ConsultaDesarrollo>(l, json)!)
            .ToList();

        var buscador = new PineconeBuscadorVectorial(
            opciones, claves, new GeneradorEmbeddingsPinecone(opciones, claves), new Lazy<CorpusNormativo>(corpus));

        var resumen = new StringBuilder(
            "consulta,tema,n_pertinentes,max_pertinente,min_pertinente,max_no_pertinente,pertinentes_sobre_theta,no_pertinentes_sobre_theta\n");
        var detalle = new StringBuilder("consulta,id,score,pertinente\n");
        var top5 = new StringBuilder("consulta,rango,id,score,pertinente\n");
        foreach (var c in consultas)
        {
            // Score de TODOS los fragmentos: topK = tamaño del corpus.
            var todos = await buscador.BuscarAsync(c.Consulta, corpus.Fragmentos.Count, ct);
            var pertinentes = todos.Where(f => c.Pertinentes.Contains(f.Id)).Select(f => f.Score).ToList();
            var otros = todos.Where(f => !c.Pertinentes.Contains(f.Id)).Select(f => f.Score).ToList();

            resumen.AppendLine(string.Join(',',
                c.Id, $"\"{c.Tema}\"", c.Pertinentes.Length,
                S(pertinentes.DefaultIfEmpty(double.NaN).Max()), S(pertinentes.DefaultIfEmpty(double.NaN).Min()),
                S(otros.DefaultIfEmpty(double.NaN).Max()),
                pertinentes.Count(s => s >= opciones.Theta), otros.Count(s => s >= opciones.Theta)));
            foreach (var f in todos)
                detalle.AppendLine(string.Join(',', c.Id, f.Id, S(f.Score), c.Pertinentes.Contains(f.Id) ? 1 : 0));
            foreach (var (f, i) in todos.OrderByDescending(f => f.Score).Take(5).Select((f, i) => (f, i)))
                top5.AppendLine(string.Join(',', c.Id, i + 1, f.Id, S(f.Score), c.Pertinentes.Contains(f.Id) ? 1 : 0));
        }

        var dir = Environment.GetEnvironmentVariable("RAG_REPORTES_DIR") ?? Path.Combine(Path.GetTempPath(), "rag");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta.csv"), resumen.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta_detalle.csv"), detalle.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta_top5.csv"), top5.ToString(), ct);
        TestContext.Current.TestOutputHelper?.WriteLine(resumen.ToString());
    }

    private static string S(double v) => double.IsNaN(v) ? "" : v.ToString("0.0000", CultureInfo.InvariantCulture);
}
