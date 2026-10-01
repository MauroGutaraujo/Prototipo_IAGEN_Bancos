using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reclamos.Application.Agentes;
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
}
