using Pinecone;
using Reclamos.Application.Rag;

namespace Reclamos.Infrastructure.Rag;

/// <summary>Clave de Pinecone (solo desde variables de entorno / user-secrets). Indexa, embebe y busca.</summary>
public sealed record ClavesRag(string? Pinecone);

/// <summary>Tipo de texto a embeber: el modelo distingue pasajes indexados de consultas.</summary>
public enum TipoEntrada
{
    Pasaje,
    Consulta,
}

/// <summary>Embeddings con el modelo fijado (SPEC §2.4); el mismo para indexar y consultar.</summary>
public interface IGeneradorEmbeddings
{
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerarAsync(IReadOnlyList<string> textos, TipoEntrada tipo, CancellationToken ct);
}

/// <summary>Embeddings de Pinecone Inference (<c>llama-text-embed-v2</c>), sin truncar.</summary>
public sealed class GeneradorEmbeddingsPinecone(RagOptions opciones, ClavesRag claves) : IGeneradorEmbeddings
{
    private readonly Lazy<PineconeClient> _cliente = new(() =>
        new PineconeClient(claves.Pinecone ?? throw new InvalidOperationException("Falta PINECONE_API_KEY")));

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerarAsync(
        IReadOnlyList<string> textos, TipoEntrada tipo, CancellationToken ct)
    {
        var respuesta = await _cliente.Value.Inference.EmbedAsync(new EmbedRequest
        {
            Model = opciones.EmbeddingModel,
            Inputs = textos.Select(t => new EmbedRequestInputsItem { Text = t }).ToList(),
            Parameters = new Dictionary<string, object?>
            {
                ["input_type"] = tipo == TipoEntrada.Consulta ? "query" : "passage",
                ["truncate"] = "NONE",
            },
        }, cancellationToken: ct);

        var vectores = respuesta.Data.Select(e => e.AsDense().Values).ToList();
        if (vectores.Count != textos.Count || vectores.Any(v => v.Length != opciones.Dimension))
        {
            throw new InvalidOperationException(
                $"{opciones.EmbeddingModel} devolvió {vectores.Count} vectores o una dimensión distinta de Rag:Dimension ({opciones.Dimension}).");
        }
        return vectores;
    }
}

/// <summary>Búsqueda en Pinecone dentro del namespace de la versión del corpus; el texto sale del corpus versionado.</summary>
public sealed class PineconeBuscadorVectorial(
    RagOptions opciones, ClavesRag claves, IGeneradorEmbeddings embeddings, Lazy<CorpusNormativo> corpus) : IBuscadorVectorial
{
    private readonly Lazy<IndexClient> _indice = new(() =>
        new PineconeClient(claves.Pinecone ?? throw new InvalidOperationException("Falta PINECONE_API_KEY"))
            .Index(opciones.Index));

    public async Task<IReadOnlyList<FragmentoRecuperado>> BuscarAsync(string consulta, int topK, CancellationToken ct)
    {
        var vector = (await embeddings.GenerarAsync([consulta], TipoEntrada.Consulta, ct))[0];
        var respuesta = await _indice.Value.QueryAsync(new QueryRequest
        {
            Vector = vector,
            TopK = (uint)topK,
            Namespace = RagOptions.Namespace(corpus.Value.Sha256),
            IncludeMetadata = false,
            IncludeValues = false,
        }, cancellationToken: ct);

        return (respuesta.Matches ?? [])
            .Select(m =>
            {
                var f = corpus.Value.PorId.TryGetValue(m.Id, out var fr)
                    ? fr
                    : throw new InvalidOperationException($"Pinecone devolvió {m.Id}, que no existe en el corpus configurado.");
                return new FragmentoRecuperado(f.Id, m.Score ?? 0f, f.Norma, f.Articulo, f.Texto);
            })
            .ToList();
    }
}
