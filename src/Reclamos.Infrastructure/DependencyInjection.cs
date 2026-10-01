using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reclamos.Application.Agentes;
using Reclamos.Application.Rag;
using Reclamos.Infrastructure.Rag;
using Reclamos.Infrastructure.Ocr;
using Reclamos.Infrastructure.Persistence;

namespace Reclamos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReclamosDbContext>(o => o.UseSqlServer(connectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<ReclamosDbContext>("sqlserver");

        return services;
    }

    /// <summary>Agente OCR (Tesseract CLI). Las opciones <see cref="OcrOptions"/> se enlazan en la API.</summary>
    public static IServiceCollection AddOcr(this IServiceCollection services)
    {
        services.AddSingleton<IOcrAgent, TesseractCliOcrAgent>();
        services.AddHealthChecks().AddCheck<TesseractHealthCheck>("tesseract");
        return services;
    }

    /// <summary>
    /// Recuperación normativa CRAG (Pinecone + embeddings OpenAI). Las opciones <see cref="RagOptions"/>
    /// se enlazan en la API; las claves llegan solo por variables de entorno / user-secrets.
    /// </summary>
    public static IServiceCollection AddRag(this IServiceCollection services, string? pineconeApiKey, string? embeddingApiKey)
    {
        services.AddSingleton(new ClavesRag(pineconeApiKey, embeddingApiKey));
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return new Lazy<CorpusNormativo>(() => CorpusNormativo.Cargar(o.CorpusPath, o.CorpusSha256));
        });
        services.AddSingleton<IGeneradorEmbeddings>(sp =>
            new GeneradorEmbeddingsOpenAI(sp.GetRequiredService<IOptions<RagOptions>>().Value, sp.GetRequiredService<ClavesRag>()));
        services.AddSingleton<IBuscadorVectorial>(sp => new PineconeBuscadorVectorial(
            sp.GetRequiredService<IOptions<RagOptions>>().Value,
            sp.GetRequiredService<ClavesRag>(),
            sp.GetRequiredService<IGeneradorEmbeddings>(),
            sp.GetRequiredService<Lazy<CorpusNormativo>>()));
        services.AddSingleton<INormativeRetriever>(sp =>
            new RecuperadorCrag(sp.GetRequiredService<IOptions<RagOptions>>().Value, sp.GetRequiredService<IBuscadorVectorial>()));
        services.AddHealthChecks().AddCheck<PineconeHealthCheck>("pinecone");
        return services;
    }
}
