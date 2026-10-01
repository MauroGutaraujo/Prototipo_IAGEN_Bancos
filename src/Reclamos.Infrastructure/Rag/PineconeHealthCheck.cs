using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Pinecone;
using Reclamos.Application.Rag;

namespace Reclamos.Infrastructure.Rag;

/// <summary>/health: el índice existe, su dimensión coincide y el corpus es el configurado.</summary>
public sealed class PineconeHealthCheck(IOptions<RagOptions> opciones, ClavesRag claves) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = opciones.Value;
        if (!o.Enabled)
            return HealthCheckResult.Healthy("RAG deshabilitado (ablación T2)");
        if (string.IsNullOrWhiteSpace(claves.Pinecone))
            return HealthCheckResult.Unhealthy("Falta PINECONE_API_KEY");

        try
        {
            var corpus = CorpusNormativo.Cargar(o.CorpusPath, o.CorpusSha256);
            var indice = await new PineconeClient(claves.Pinecone).DescribeIndexAsync(o.Index, cancellationToken: cancellationToken);
            if (indice.Dimension != o.Dimension)
                return HealthCheckResult.Unhealthy($"El índice {o.Index} tiene dimensión {indice.Dimension}; se esperaba {o.Dimension}");
            return HealthCheckResult.Healthy($"{o.Index} ({RagOptions.Namespace(corpus.Sha256)}, {corpus.Fragmentos.Count} fragmentos)");
        }
        catch (NotFoundError)
        {
            return HealthCheckResult.Unhealthy($"No existe el índice {o.Index}: ejecuta el indexador");
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or PineconeApiException or HttpRequestException)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
