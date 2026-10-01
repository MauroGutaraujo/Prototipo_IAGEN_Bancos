using Reclamos.Domain.Enums;

namespace Reclamos.Guardrails.Legal;

/// <summary>Salida del Agente OCR: valores extraídos (null si no cumplen su regex) y confianza c_f (0–1).</summary>
public sealed record ResultadoOcr(
    decimal? Monto,
    Moneda? Moneda,
    DateTime? FechaHora,
    string? Codigo,
    decimal ConfMonto,
    decimal ConfFecha,
    decimal ConfCodigo);

/// <summary>Salida validada del clasificador. Si <see cref="EsValida"/> es false, sus datos se ignoran.</summary>
public sealed record SalidaClasificador(
    bool EsValida,
    Intencion? Intencion,
    decimal? Monto,
    Moneda? Moneda,
    DateOnly? Fecha,
    string? Codigo);

/// <summary>Transacción del núcleo transaccional simulado.</summary>
public sealed record TransaccionCore(
    string Codigo,
    int IdCliente,
    decimal Monto,
    Moneda Moneda,
    DateTime FechaHora,
    string Comercio,
    EstadoTransaccion Estado,
    bool AutenticacionReforzada,
    string? Dispositivo,
    bool IndicadorRiesgo);

/// <summary>Hechos del core para el expediente.</summary>
/// <param name="MovimientosCliente">Cargos del mismo cliente (puede incluir la propia transacción).</param>
/// <param name="TipoCambioUsdPen">Tipo de cambio del día de la operación; null si no existe.</param>
public sealed record HechosCore(
    TransaccionCore Transaccion,
    IReadOnlyList<TransaccionCore> MovimientosCliente,
    decimal? TipoCambioUsdPen,
    string DispositivoRegistrado);

public sealed record EntradaLegal(ResultadoOcr Ocr, SalidaClasificador Clasificador, HechosCore Core);
