// Indexa knowledge/fragmentos.jsonl en Pinecone (SPEC §2.4).
// Claves solo por variables de entorno: PINECONE_API_KEY, EMBEDDING_API_KEY (y opcional PINECONE_INDEX).
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

const string Nube = "aws";
const string Region = "us-east-1";
const int Lote = 50;

var claves = new ClavesRag(
    Environment.GetEnvironmentVariable("PINECONE_API_KEY"),
    Environment.GetEnvironmentVariable("EMBEDDING_API_KEY"));
if (string.IsNullOrWhiteSpace(claves.Pinecone) || string.IsNullOrWhiteSpace(claves.Embeddings))
{
    Error("Faltan PINECONE_API_KEY y/o EMBEDDING_API_KEY.");
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
Pinecone.Index indice;
try
{
    indice = await pinecone.DescribeIndexAsync(opciones.Index);
}
catch (NotFoundError)
{
    Console.WriteLine($"Creando índice {opciones.Index} (serverless {Nube}/{Region}, cosine, {opciones.Dimension})");
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

if (indice.Dimension != opciones.Dimension || indice.Metric != IndexModelMetric.Cosine)
{
    Error($"El índice existe con dimensión {indice.Dimension} y métrica {indice.Metric}; se esperaba {opciones.Dimension}/cosine.");
    return 1;
}

var embeddings = new GeneradorEmbeddingsOpenAI(opciones, claves);
var cliente = pinecone.Index(opciones.Index);
var total = 0;
foreach (var lote in corpus.Fragmentos.Chunk(Lote))
{
    // Se embebe el nombre de la norma junto con el texto del artículo.
    var vectores = await embeddings.GenerarAsync(lote.Select(f => $"{f.Norma}\n{f.Texto}").ToList(), CancellationToken.None);
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

Console.WriteLine($"Indexados {total} fragmentos en {opciones.Index}/{ns} con {opciones.EmbeddingModel}.");
return 0;
