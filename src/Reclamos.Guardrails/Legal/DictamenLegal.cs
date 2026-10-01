using Reclamos.Domain.Enums;

namespace Reclamos.Guardrails.Legal;

/// <summary>Decisión del Agente Legal: ruta, regla que disparó y motivo.</summary>
public sealed record DictamenLegal(Ruta Ruta, string Regla, string Motivo);

/// <summary>Códigos de motivo persistidos en <c>app.DecisionLegal.Motivo</c>.</summary>
public static class MotivoLegal
{
    public const string OcrIlegible = "OCR_ILEGIBLE";
    public const string Discrepancia = "DISCREPANCIA";
    public const string MontoSuperaUmbral = "MONTO_SUPERA_UMBRAL";
    public const string TipoCambioNoDisponible = "TIPO_CAMBIO_NO_DISPONIBLE";
    public const string ClasificadorInvalido = "CLASIFICADOR_INVALIDO";
    public const string FueraDeCatalogo = "FUERA_DE_CATALOGO";
    public const string CargoDuplicado = "CARGO_DUPLICADO";
    public const string SinCargoDuplicado = "SIN_CARGO_DUPLICADO";
    public const string OperacionNoCompletada = "OPERACION_NO_COMPLETADA";
    public const string Conciliacion = "CONCILIACION";
    public const string SinAutenticacionReforzada = "SIN_AUTENTICACION_REFORZADA";
    public const string IndicadorRiesgo = "INDICADOR_RIESGO";
    public const string DispositivoNoRegistrado = "DISPOSITIVO_NO_REGISTRADO";
    public const string OperacionAutenticada = "OPERACION_AUTENTICADA";
}
