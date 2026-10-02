using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reclamos.Application.Agentes;
using Reclamos.Application.Llm;
using Reclamos.Application.Orquestacion;
using Reclamos.Guardrails;
using Reclamos.Infrastructure.Llm;
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
    /// Recuperación normativa CRAG (Pinecone: embeddings de Pinecone Inference + búsqueda). Las opciones <see cref="RagOptions"/>
    /// se enlazan en la API; las claves llegan solo por variables de entorno / user-secrets.
    /// </summary>
    public static IServiceCollection AddRag(this IServiceCollection services, string? pineconeApiKey)
    {
        services.AddSingleton(new ClavesRag(pineconeApiKey));
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<RagOptions>>().Value;
            return new Lazy<CorpusNormativo>(() => CorpusNormativo.Cargar(o.CorpusPath, o.CorpusSha256));
        });
        services.AddSingleton<IGeneradorEmbeddings>(sp =>
            new GeneradorEmbeddingsPinecone(sp.GetRequiredService<IOptions<RagOptions>>().Value, sp.GetRequiredService<ClavesRag>()));
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

    /// <summary>
    /// Clasificador, generador (Semantic Kernel) y orquestador. Las opciones <see cref="LlmOptions"/>,
    /// <see cref="BancoOptions"/> y <see cref="GuardrailOptions"/> se enlazan en la API.
    /// </summary>
    public static IServiceCollection AddOrquestacion(this IServiceCollection services, string directorioPrompts = "prompts")
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => RegistroModelos.Desde(File.ReadAllText(
            Rutas.ResolverArchivo(sp.GetRequiredService<IOptions<LlmOptions>>().Value.RegistroPath))));
        services.AddSingleton(_ => new RepositorioPrompts(Rutas.ResolverDirectorio(directorioPrompts)));
        services.AddSingleton<IClienteLlm>(sp => new ClienteLlmSemanticKernel(
            sp.GetRequiredService<RegistroModelos>(), sp.GetRequiredService<IOptions<LlmOptions>>().Value));
        services.AddSingleton<IIntentClassifier>(sp =>
        {
            var prompts = sp.GetRequiredService<RepositorioPrompts>();
            return new ClasificadorIntencion(sp.GetRequiredService<IClienteLlm>(), prompts.Cargar("clasificador.v1"),
                prompts.LeerTexto("clasificador.schema.json"), sp.GetRequiredService<RegistroModelos>().Parametros);
        });
        services.AddSingleton<IResolutionGenerator>(sp =>
        {
            var prompts = sp.GetRequiredService<RepositorioPrompts>();
            return new GeneradorResolucion(sp.GetRequiredService<IClienteLlm>(), prompts.Cargar("generador.v2"),
                prompts.Cargar("regeneracion.v1"), sp.GetRequiredService<RegistroModelos>().Parametros,
                sp.GetRequiredService<IOptions<GuardrailOptions>>().Value.PlazoRespuesta);
        });
        services.AddScoped<IFuenteExpedientes>(sp =>
            new FuenteExpedientesEf(sp.GetRequiredService<ReclamosDbContext>(), sp.GetRequiredService<IOptions<BancoOptions>>().Value));
        services.AddScoped<IAlmacenEjecuciones, AlmacenEjecucionesEf>();
        services.AddScoped<OrquestadorExpedientes>();
        return services;
    }
}
