using Reclamos.Domain.Enums;

namespace Reclamos.Domain.Evaluacion;

/// <summary>
/// Verdad de referencia del banco sintético (esquema `eval`).
/// Solo la leen tools/analysis y los tests de evaluación; ningún agente
/// del sistema la consulta en tiempo de ejecución.
/// </summary>
public class VerdadReferencia
{
    public int IdExpediente { get; set; }
    public Intencion IntencionReal { get; set; }
    public decimal? MontoReal { get; set; }
    public Moneda? MonedaReal { get; set; }
    public DateTime? FechaReal { get; set; }
    public string? CodigoReal { get; set; }
    /// <summary>P. ej. "monto,fecha,codigo".</summary>
    public required string CamposLegibles { get; set; }
    public Ruta RutaCorrecta { get; set; }
    public required string ReglaEsperada { get; set; }
}
