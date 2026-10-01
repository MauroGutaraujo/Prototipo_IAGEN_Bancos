using Reclamos.Domain.Enums;

namespace Reclamos.Guardrails.Salida;

/// <summary>Hechos verificados que el borrador puede mencionar.</summary>
public sealed record HechosVerificados(int NumeroReclamo, decimal Monto, Moneda Moneda, DateTime FechaHora, string Codigo);

/// <param name="Ruta">Ruta del Agente Legal; solo Procedente o Improcedente llegan a redacción.</param>
/// <param name="FragmentosAdmitidos">Ids de fragmentos que el borrador puede citar (vacío en ablación T2).</param>
/// <param name="ModoAblacion">T2: se evalúa y registra, pero no bloquea.</param>
public sealed record EntradaGuardrailSalida(
    string Borrador,
    Ruta Ruta,
    HechosVerificados Hechos,
    IReadOnlyCollection<string> FragmentosAdmitidos,
    bool ModoAblacion);

public enum TipoFalla
{
    CitaNoAdmitida,
    HechoNoCoincide,
    SinMarcadorDecision,
    SentidoContradictorio,
    FaltaNumeroReclamo,
    FaltaInstancia,
    FaltaPlazo,
}

public sealed record FallaGuardrail(TipoFalla Tipo, string Detalle);

/// <summary>Veredicto del guardrail de salida y conteos para TA (SPEC §2.6).</summary>
public sealed record VeredictoGuardrail(
    IReadOnlyList<FallaGuardrail> Fallas,
    int AfirmacionesVerificables,
    int AfirmacionesNoSustentadas,
    bool ModoAblacion)
{
    public bool Aprobado => Fallas.Count == 0;

    /// <summary>En ablación T2 el guardrail nunca bloquea.</summary>
    public bool Bloquea => !Aprobado && !ModoAblacion;
}
