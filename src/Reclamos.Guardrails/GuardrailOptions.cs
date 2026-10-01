namespace Reclamos.Guardrails;

/// <summary>Parámetros de la tesis para el Agente Legal y el guardrail de salida (sección <c>Guardrail</c>).</summary>
public sealed class GuardrailOptions
{
    public const string Seccion = "Guardrail";

    /// <summary>U_ocr: confianza mínima normalizada (0–1) de cada campo OCR. Igual al umbral es válido.</summary>
    public decimal UmbralOcr { get; set; } = 0.90m;

    /// <summary>U_riesgo: monto en soles por encima del cual se deriva (R3).</summary>
    public decimal UmbralRiesgoPen { get; set; } = 1000.00m;

    /// <summary>Ventana para considerar duplicado un cargo (R5). |Δt| igual a la ventana cuenta como duplicado.</summary>
    public int VentanaDuplicadoHoras { get; set; } = 24;

    /// <summary>
    /// Plazo que la resolución debe mencionar tal cual (SPEC §2.6.4). Lo fija el estudiante desde la
    /// normativa vigente; no tiene valor por defecto.
    /// </summary>
    public string PlazoRespuesta { get; set; } = "";
}
