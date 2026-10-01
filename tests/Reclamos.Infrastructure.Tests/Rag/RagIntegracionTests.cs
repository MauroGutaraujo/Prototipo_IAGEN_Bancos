using System.Globalization;
using System.Text;
using System.Text.Json;
using Pinecone;
using Reclamos.Application.Rag;
using Reclamos.Domain.Enums;
using Reclamos.Infrastructure.Rag;

namespace Reclamos.Infrastructure.Tests.Rag;

/// <summary>
/// H5: tres consultas reales (una por tipología) contra Pinecone; REPORTA ids y scores, sin umbrales
/// de desempeño. Se omite si faltan los secrets o el índice aún no existe.
/// </summary>
[Trait("Categoria", "IntegracionRag")]
public class RagIntegracionTests
{
    private static readonly ConsultaNormativa[] Consultas =
    [
        new(Intencion.C1, Ruta.Procedente, "R5"),
        new(Intencion.C2, Ruta.Improcedente, "R9"),
        new(Intencion.C3, Ruta.Procedente, "R7"),
    ];

    [Fact]
    public async Task Tres_consultas_una_por_tipologia()
    {
        var claves = new ClavesRag(Environment.GetEnvironmentVariable("PINECONE_API_KEY"));
        Assert.SkipWhen(string.IsNullOrWhiteSpace(claves.Pinecone), "Falta PINECONE_API_KEY");

        var ct = TestContext.Current.CancellationToken;
        var opciones = new RagOptions();
        if (Environment.GetEnvironmentVariable("PINECONE_INDEX") is { Length: > 0 } indice)
            opciones.Index = indice;

        var sha = JsonDocument.Parse(await File.ReadAllTextAsync(CorpusNormativo.ResolverRuta("knowledge/manifest.json"), ct))
            .RootElement.GetProperty("fragmentos_sha256").GetString()!;
        var corpus = CorpusNormativo.Cargar(opciones.CorpusPath, sha);

        try
        {
            await new PineconeClient(claves.Pinecone!).DescribeIndexAsync(opciones.Index, cancellationToken: ct);
        }
        catch (NotFoundError)
        {
            Assert.Skip($"El índice {opciones.Index} no existe todavía: ejecuta el indexador");
        }

        var buscador = new PineconeBuscadorVectorial(
            opciones, claves, new GeneradorEmbeddingsPinecone(opciones, claves), new Lazy<CorpusNormativo>(corpus));
        var crag = new RecuperadorCrag(opciones, buscador);

        var csv = new StringBuilder("combinacion,calificacion,consulta,id,score,admitido\n");
        foreach (var consulta in Consultas)
        {
            var r = await crag.RecuperarAsync(consulta, ct);
            Assert.NotEmpty(r.Evaluados); // funcional: Pinecone respondió; los scores solo se reportan
            foreach (var f in r.Evaluados)
            {
                csv.AppendLine(string.Join(',',
                    $"{consulta.Intencion}-{consulta.Regla}", r.Calificacion, f.Consulta, f.Id,
                    f.Score.ToString("0.0000", CultureInfo.InvariantCulture), f.Admitido ? 1 : 0));
            }
        }

        var dir = Environment.GetEnvironmentVariable("RAG_REPORTES_DIR") ?? Path.Combine(Path.GetTempPath(), "rag");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "rag_integracion.csv"), csv.ToString(), ct);
        TestContext.Current.TestOutputHelper?.WriteLine(csv.ToString());
    }
}
