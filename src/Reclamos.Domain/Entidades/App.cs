using Reclamos.Domain.Enums;

namespace Reclamos.Domain.Entidades;

// Esquema `app`: expedientes, ejecuciones del pipeline y auditoría.

public class Expediente
{
    public int IdExpediente { get; set; }
    public required string CodigoOperacion { get; set; }
    public required string NarracionCliente { get; set; }
    public DateTime FechaIngreso { get; set; }

    public Transaccion? Transaccion { get; set; }
    public ICollection<Evidencia> Evidencias { get; set; } = [];
}

public class Evidencia
{
    public int IdEvidencia { get; set; }
    public int IdExpediente { get; set; }
    public required string RutaImagen { get; set; }
    public required string PerturbacionAplicada { get; set; }

    public Expediente? Expediente { get; set; }
}

public class CorridaBenchmark
{
    public int IdCorrida { get; set; }
    public Condicion Condicion { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Una fila por expediente × modelo × repetición × condición (SPEC §3.2).</summary>
public class Ejecucion
{
    public long IdEjecucion { get; set; }
    public int? IdCorrida { get; set; }
    public int IdExpediente { get; set; }
    public Condicion Condicion { get; set; }
    public required string ModeloId { get; set; }
    public string? ModeloVersion { get; set; }
    public required string PromptVersion { get; set; }
    public byte Repeticion { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }

    public int T_Ocr_ms { get; set; }
    public int T_Guardrail_ms { get; set; }
    public int T_Rag_ms { get; set; }
    public int L_Cls_ms { get; set; }
    public int L_Gen_ms { get; set; }
    public int T_Orq_ms { get; set; }
    public int L_Total_ms { get; set; }

    public int? TokensIn { get; set; }
    public int? TokensOut { get; set; }
    public EstadoExpediente EstadoFinal { get; set; }
    public Intencion? IntencionPredicha { get; set; }

    public decimal? MontoExtraido { get; set; }
    public DateTime? FechaExtraida { get; set; }
    public string? CodigoExtraido { get; set; }
    public decimal? ConfMonto { get; set; }
    public decimal? ConfFecha { get; set; }
    public decimal? ConfCodigo { get; set; }

    public byte Regeneraciones { get; set; }

    /// <summary>R1–R4, R7, R8, CRAG_INCORRECTA, GUARDRAIL_SALIDA o ERROR; null si se emitió.</summary>
    public string? MotivoDerivacion { get; set; }

    public CorridaBenchmark? Corrida { get; set; }
    public Expediente? Expediente { get; set; }
    public ICollection<TransicionEstado> Transiciones { get; set; } = [];
    public DecisionLegal? DecisionLegal { get; set; }
    public Recuperacion? Recuperacion { get; set; }
    public Resolucion? Resolucion { get; set; }
}

public class TransicionEstado
{
    public long IdTransicion { get; set; }
    public long IdEjecucion { get; set; }
    public EstadoExpediente Estado { get; set; }
    public DateTime MarcaUtc { get; set; }
}

public class DecisionLegal
{
    public long IdEjecucion { get; set; }
    public Ruta Ruta { get; set; }
    public required string Regla { get; set; }
    public required string Motivo { get; set; }
}

public class Recuperacion
{
    public long IdEjecucion { get; set; }
    public CalificacionRecuperacion Calificacion { get; set; }
    /// <summary>JSON: [{id, score, admitido}].</summary>
    public required string FragmentosJson { get; set; }
}

public class Resolucion
{
    public long IdEjecucion { get; set; }
    public string? TextoBorrador { get; set; }
    public string? TextoFinal { get; set; }
    public bool AprobadaGuardrail { get; set; }
    public int? AfirmacionesVerificables { get; set; }
    public int? AfirmacionesNoSustentadas { get; set; }
    /// <summary>Lo completa la revisión normativa.</summary>
    public bool? ConformeChecklist { get; set; }
}

/// <summary>Registro de la ficha de cronometraje T0 (SPEC §5).</summary>
public class RegistroManual
{
    public int IdRegistro { get; set; }
    public int IdExpediente { get; set; }
    /// <summary>A1 | A2 | A3.</summary>
    public required string CodigoAnalista { get; set; }
    public bool EsCalibracion { get; set; }
    public DateTime TIngresoUtc { get; set; }
    public DateTime TFinalUtc { get; set; }
    public int PausasMs { get; set; }
    public Intencion? Intencion { get; set; }
    public decimal? Monto { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? Codigo { get; set; }
    public Ruta? Ruta { get; set; }
    public string? TextoRespuesta { get; set; }

    public Expediente? Expediente { get; set; }
}
