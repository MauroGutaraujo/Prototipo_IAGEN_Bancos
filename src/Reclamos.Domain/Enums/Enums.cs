namespace Reclamos.Domain.Enums;

/// <summary>Moneda de la operación (código ISO 4217).</summary>
public enum Moneda
{
    PEN,
    USD,
}

/// <summary>Estado de la transacción en el núcleo transaccional simulado.</summary>
public enum EstadoTransaccion
{
    Completada,
    Fallida,
    NoCompletada,
}

/// <summary>Clases de intención del reclamo (SPEC §2.2).</summary>
public enum Intencion
{
    /// <summary>Cobro duplicado.</summary>
    C1,
    /// <summary>Operación no reconocida.</summary>
    C2,
    /// <summary>Caída de pasarela.</summary>
    C3,
    FueraDeCatalogo,
}

/// <summary>Ruta decidida por el Agente Legal (SPEC §2.3).</summary>
public enum Ruta
{
    Derivar,
    Procedente,
    Improcedente,
}

/// <summary>Condición experimental: T0 manual, T1 sistema completo, T2 ablación sin RAG.</summary>
public enum Condicion
{
    T0,
    T1,
    T2,
}

/// <summary>Estados de la máquina de estados del expediente (SPEC §1).</summary>
public enum EstadoExpediente
{
    Recibido,
    OcrExtraido,
    Clasificado,
    Decidido,
    Fundamentado,
    Redactado,
    Verificado,
    Emitido,
    Derivado,
    Error,
}

/// <summary>Calificación CRAG de la recuperación normativa (SPEC §2.4).</summary>
public enum CalificacionRecuperacion
{
    Correcta,
    Ambigua,
    Incorrecta,
    Omitida,
}
