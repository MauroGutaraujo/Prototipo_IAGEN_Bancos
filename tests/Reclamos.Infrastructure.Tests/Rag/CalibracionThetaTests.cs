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
        // Top-K de cada consulta, tal como lo ve el CRAG en ejecución (para el barrido de θ).
        var recuperados = new List<(ConsultaDesarrollo Consulta, List<FragmentoRecuperado> TopK)>();
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
            recuperados.Add((c, todos.OrderByDescending(f => f.Score).Take(opciones.TopK).ToList()));
        }

        // Barrido de θ sobre el top-K: cuánto de lo pertinente se admite y cuánto ruido entra.
        var positivas = recuperados.Where(r => r.Consulta.Pertinentes.Length > 0).ToList();
        var negativas = recuperados.Where(r => r.Consulta.Pertinentes.Length == 0).ToList();
        var totalPertinentes = positivas.Sum(r => r.Consulta.Pertinentes.Length);
        var barrido = new StringBuilder(
            "theta,consultas_con_pertinente_admitido,consultas_positivas,pertinentes_admitidos,pertinentes_etiquetados," +
            "no_pertinentes_admitidos,admitidos_en_controles_negativos,precision,recall\n");
        for (var paso = 0; paso <= 6; paso++)
        {
            var theta = 0.30 + paso * 0.05;
            int conPertinente = 0, tp = 0, fp = 0;
            foreach (var (c, topK) in positivas)
            {
                var admitidos = topK.Where(f => f.Score >= theta).ToList();
                var verdaderos = admitidos.Count(f => c.Pertinentes.Contains(f.Id));
                conPertinente += verdaderos > 0 ? 1 : 0;
                tp += verdaderos;
                fp += admitidos.Count - verdaderos;
            }
            var enNegativos = negativas.Sum(r => r.TopK.Count(f => f.Score >= theta));
            barrido.AppendLine(string.Join(',',
                theta.ToString("0.00", CultureInfo.InvariantCulture), conPertinente, positivas.Count, tp, totalPertinentes,
                fp, enNegativos,
                tp + fp == 0 ? "" : S((double)tp / (tp + fp)), S((double)tp / totalPertinentes)));
        }

        var dir = Environment.GetEnvironmentVariable("RAG_REPORTES_DIR") ?? Path.Combine(Path.GetTempPath(), "rag");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta.csv"), resumen.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta_detalle.csv"), detalle.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta_top5.csv"), top5.ToString(), ct);
        await File.WriteAllTextAsync(Path.Combine(dir, "calibracion_theta_barrido.csv"), barrido.ToString(), ct);
        TestContext.Current.TestOutputHelper?.WriteLine(resumen.ToString());
    }

    private static string S(double v) => double.IsNaN(v) ? "" : v.ToString("0.0000", CultureInfo.InvariantCulture);
}
