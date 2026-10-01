// Indexa knowledge/fragmentos.jsonl en Pinecone (SPEC §2.4).
// Clave solo por variable de entorno: PINECONE_API_KEY (y opcional PINECONE_INDEX). Embeddings: Pinecone Inference.
// Idempotente: ids estables por fragmento y un namespace por versión del corpus (corpus-<sha256[..12]>).

using System.Text.Json;
using Pinecone;
using Reclamos.Application.Rag;
using Reclamos.Infrastructure.Rag;

var enCi = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
void Error(string mensaje)
{
    Console.Error.WriteLine(mensaje);
    if (enCi)
        Console.WriteLine($"::error title=Indexador::{mensaje.ReplaceLineEndings(" ")}");
}

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Error($"{e.ExceptionObject.GetType().Name}: {(e.ExceptionObject as Exception)?.Message}");

const string Region = "us-east-1";
const int Lote = 50;

var claves = new ClavesRag(Environment.GetEnvironmentVariable("PINECONE_API_KEY"));
if (string.IsNullOrWhiteSpace(claves.Pinecone))
{
    Error("Falta PINECONE_API_KEY.");
    return 1;
}

var opciones = new RagOptions();
if (Environment.GetEnvironmentVariable("PINECONE_INDEX") is { Length: > 0 } nombreIndice)
    opciones.Index = nombreIndice;

var manifiesto = JsonDocument.Parse(File.ReadAllText(CorpusNormativo.ResolverRuta("knowledge/manifest.json")));
var shaEsperado = manifiesto.RootElement.GetProperty("fragmentos_sha256").GetString()!;
var corpus = CorpusNormativo.Cargar(opciones.CorpusPath, shaEsperado);
var ns = RagOptions.Namespace(corpus.Sha256);
Console.WriteLine($"Corpus: {corpus.Fragmentos.Count} fragmentos, SHA-256 {corpus.Sha256} → namespace {ns}");

var pinecone = new PineconeClient(claves.Pinecone);
var indice = await DescribirAsync();

// Un índice con otra dimensión o métrica solo se recrea si está VACÍO (p. ej. el que dejó una corrida fallida).
if (indice is not null && (indice.Dimension != opciones.Dimension || indice.Metric != IndexModelMetric.Cosine))
{
    var stats = await pinecone.Index(opciones.Index).DescribeIndexStatsAsync(new DescribeIndexStatsRequest());
    if ((stats.TotalVectorCount ?? 0) > 0)
    {
        Error($"El índice {opciones.Index} tiene dimensión {indice.Dimension}/{indice.Metric} y {stats.TotalVectorCount} vectores; " +
              $"se esperaba {opciones.Dimension}/cosine. No se borra un índice con datos: usa otro nombre (PINECONE_INDEX).");
        return 1;
    }
    Console.WriteLine($"El índice {opciones.Index} está vacío y tiene dimensión {indice.Dimension}: se elimina y se recrea.");
    await pinecone.DeleteIndexAsync(opciones.Index);
    for (var intento = 0; (indice = await DescribirAsync()) is not null; intento++)
    {
        if (intento == 60)
        {
            Error("El índice no terminó de eliminarse en 2 minutos.");
            return 1;
        }
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

if (indice is null)
{
    Console.WriteLine($"Creando índice {opciones.Index} (serverless aws/{Region}, cosine, {opciones.Dimension})");
    indice = await pinecone.CreateIndexAsync(new CreateIndexRequest
    {
        Name = opciones.Index,
        Dimension = opciones.Dimension,
        Metric = MetricType.Cosine,
        Spec = new ServerlessIndexSpec { Serverless = new ServerlessSpec { Cloud = ServerlessSpecCloud.Aws, Region = Region } },
        DeletionProtection = DeletionProtection.Disabled,
    });
}

for (var intento = 0; !indice.Status.Ready; intento++)
{
    if (intento == 60)
    {
        Error("El índice no quedó listo en 2 minutos.");
        return 1;
    }
    await Task.Delay(TimeSpan.FromSeconds(2));
    indice = await pinecone.DescribeIndexAsync(opciones.Index);
}

var embeddings = new GeneradorEmbeddingsPinecone(opciones, claves);
var cliente = pinecone.Index(opciones.Index);
var total = 0;
foreach (var lote in corpus.Fragmentos.Chunk(Lote))
{
    // Se embebe el nombre de la norma junto con el texto del artículo (input_type = passage).
    var textos = lote.Select(f => f.Norma + "\n" + f.Texto).ToList();
    var vectores = await embeddings.GenerarAsync(textos, TipoEntrada.Pasaje, CancellationToken.None);
    await cliente.UpsertAsync(new UpsertRequest
    {
        Namespace = ns,
        Vectors = lote.Select((f, i) => new Vector
        {
            Id = f.Id,
            Values = vectores[i],
            Metadata = new Metadata
            {
                ["norma"] = f.Norma,
                ["articulo"] = f.Articulo,
                ["tipo"] = f.Tipo,
                ["corpus_sha256"] = corpus.Sha256,
            },
        }).ToList(),
    });
    total += lote.Length;
}

// La búsqueda serverless es eventualmente consistente: se espera a ver todos los vectores en el namespace.
for (var intento = 0; intento < 30; intento++)
{
    var stats = await cliente.DescribeIndexStatsAsync(new DescribeIndexStatsRequest());
    if (stats.Namespaces is { } n && n.TryGetValue(ns, out var resumen) && resumen.VectorCount >= total)
        break;
    await Task.Delay(TimeSpan.FromSeconds(2));
}

Console.WriteLine($"Indexados {total} fragmentos en {opciones.Index}/{ns} con {opciones.EmbeddingModel}.");
return 0;

async Task<Pinecone.Index?> DescribirAsync()
{
    try
    {
        return await pinecone.DescribeIndexAsync(opciones.Index);
    }
    catch (NotFoundError)
    {
        return null;
    }
}
