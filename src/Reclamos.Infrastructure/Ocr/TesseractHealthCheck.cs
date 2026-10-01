using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Reclamos.Application.Agentes;

namespace Reclamos.Infrastructure.Ocr;

/// <summary>/health: Tesseract responde y el modelo de idioma es el fijado (SHA-256).</summary>
public sealed class TesseractHealthCheck(IOptions<OcrOptions> opciones, IServiceProvider servicios) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = opciones.Value;
        if (string.IsNullOrWhiteSpace(o.TessdataDir))
            return HealthCheckResult.Unhealthy("Ocr:TessdataDir no configurado");

        var sha = await TesseractCliOcrAgent.Sha256ModeloAsync(o, cancellationToken);
        if (sha is null)
            return HealthCheckResult.Unhealthy($"No existe {o.Idioma}.traineddata en Ocr:TessdataDir");
        if (!string.Equals(sha, o.ModeloSha256, StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Unhealthy($"SHA-256 del modelo {sha} distinto del fijado en Ocr:ModeloSha256");

        var agente = (IOcrAgent)servicios.GetService(typeof(IOcrAgent))!;
        if (agente is not TesseractCliOcrAgent tesseract)
            return HealthCheckResult.Healthy("Agente OCR no basado en Tesseract");
        var motor = await tesseract.MotorAsync(cancellationToken);
        return motor.VersionTesseract == "desconocida"
            ? HealthCheckResult.Unhealthy($"No se pudo ejecutar {o.RutaTesseract}")
            : HealthCheckResult.Healthy(motor.VersionTesseract);
    }
}
