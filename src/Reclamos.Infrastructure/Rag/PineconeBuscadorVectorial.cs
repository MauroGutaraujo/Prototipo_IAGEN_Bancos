using OpenAI.Embeddings;
using Pinecone;
using Reclamos.Application.Rag;

namespace Reclamos.Infrastructure.Rag;

/// <summary>Claves de los servicios externos (solo desde variables de entorno / user-secrets).</summary>
public sealed record ClavesRag(string? Pinecone, string? Embeddings);

/// <summary>Embeddings con el modelo fijado (SPEC §2.4); el mismo para indexar y consultar.</summary>
public interface IGeneradorEmbeddings
{
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerarAsync(IReadOnlyList<string> textos, CancellationToken ct);
}

public sealed class GeneradorEmbeddingsOpenAI(RagOptions opciones, ClavesRag claves) : IGeneradorEmbeddings
{
    private readonly Lazy<EmbeddingClient> _cliente = new(() => new EmbeddingClient(
        opciones.EmbeddingModel,
        claves.Embeddings ?? throw new InvalidOperationException("Falta EMBEDDING_API_KEY")));

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerarAsync(IReadOnlyList<string> textos, CancellationToken ct)
    {
        var respuesta = await _cliente.Value.GenerateEmbeddingsAsync(textos, cancellationToken: ct);
        var vectores = respuesta.Value.Select(e => e.ToFloats()).ToList();
        if (vectores.Any(v => v.Length != opciones.Dimension))
        {
            throw new InvalidOperationException(
                $"El modelo {opciones.EmbeddingModel} devolvió una dimensión distinta de Rag:Dimension ({opciones.Dimension}).");
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
        var vector = (await embeddings.GenerarAsync([consulta], ct))[0];
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
