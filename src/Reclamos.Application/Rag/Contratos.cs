using Reclamos.Domain.Enums;

namespace Reclamos.Application.Rag;

/// <summary>Parámetros de la recuperación normativa (sección <c>Rag</c>, SPEC §2.4).</summary>
public sealed class RagOptions
{
    public const string Seccion = "Rag";

    /// <summary>false = ablación T2: no se consulta Pinecone (calificación Omitida).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>θ: score coseno mínimo para admitir un fragmento (igual a θ se admite).</summary>
    public double Theta { get; set; } = 0.78;

    public int TopK { get; set; } = 5;

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public int Dimension { get; set; } = 1536;

    public string Index { get; set; } = "normativa-reclamos";

    /// <summary>Ruta de knowledge/fragmentos.jsonl.</summary>
    public string CorpusPath { get; set; } = "knowledge/fragmentos.jsonl";

    /// <summary>SHA-256 esperado del corpus (knowledge/manifest.json). Define el namespace en Pinecone.</summary>
    public string CorpusSha256 { get; set; } = "";

    /// <summary>Namespace de Pinecone de la versión del corpus: <c>corpus-&lt;12 hex&gt;</c>.</summary>
    public static string Namespace(string corpusSha256) => $"corpus-{corpusSha256[..12]}";
}

/// <summary>Lo que el Agente Legal decidió; de aquí sale la consulta normativa.</summary>
public sealed record ConsultaNormativa(Intencion Intencion, Ruta Ruta, string Regla);

/// <summary>Fragmento del corpus con su score coseno para una consulta.</summary>
public sealed record FragmentoRecuperado(string Id, double Score, string Norma, string Articulo, string Texto);

/// <summary>Registro de auditoría de cada fragmento evaluado (va a FragmentosJson).</summary>
public sealed record FragmentoEvaluado(string Id, double Score, bool Admitido, int Consulta);

/// <summary>Búsqueda vectorial (embeddings + Pinecone) de una consulta en texto.</summary>
public interface IBuscadorVectorial
{
    Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(string consulta, int topK, CancellationToken ct);
}

/// <summary>Recuperación normativa con control CRAG (SPEC §2.4).</summary>
public interface INormativeRetriever
{
    Task<ResultadoRecuperacion> RecuperarAsync(ConsultaNormativa consulta, CancellationToken ct);
}
