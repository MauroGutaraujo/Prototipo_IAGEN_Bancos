using Reclamos.Guardrails.Legal;

namespace Reclamos.Application.Agentes;

/// <summary>Agente OCR: extrae monto, fecha y código del voucher con su confianza (SPEC §2.1).</summary>
public interface IOcrAgent
{
    Task<LecturaOcr> ExtraerAsync(string rutaImagen, CancellationToken ct);
}

/// <summary>Versión del motor usada en la lectura (se registra en cada corrida).</summary>
public sealed record MotorOcr(string VersionTesseract, string ModeloSha256);

/// <param name="Resultado">Campos y confianzas; con error, todos nulos (R1 deriva).</param>
/// <param name="TsvCrudo">Salida TSV de Tesseract, para auditoría.</param>
/// <param name="Error">Motivo si la lectura falló; null si Tesseract respondió.</param>
public sealed record LecturaOcr(ResultadoOcr Resultado, string TsvCrudo, string? Error, MotorOcr Motor);
