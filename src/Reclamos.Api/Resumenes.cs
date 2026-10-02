using Microsoft.EntityFrameworkCore;
using Reclamos.Domain.Entidades;
using Reclamos.Infrastructure.Persistence;

namespace Reclamos.Api;

/// <summary>Vista de una ejecución para la API (sin datos de evaluación).</summary>
public static class Resumenes
{
    public static IQueryable<Ejecucion> Consulta(ReclamosDbContext db) => db.Ejecuciones.AsNoTracking()
        .Include(e => e.DecisionLegal)
        .Include(e => e.Recuperacion)
        .Include(e => e.Resolucion)
        .Include(e => e.Transiciones);

    public static object De(Ejecucion e, string? error) => new
    {
        e.IdEjecucion,
        e.IdExpediente,
        e.Condicion,
        e.ModeloId,
        e.ModeloVersion,
        e.Repeticion,
        e.PromptVersion,
        e.EstadoFinal,
        e.MotivoDerivacion,
        error,
        decision = e.DecisionLegal is { } d ? new { d.Ruta, d.Regla, d.Motivo } : null,
        recuperacion = e.Recuperacion is { } r ? new { r.Calificacion, r.FragmentosJson } : null,
        resolucion = e.Resolucion is { } s
            ? new { s.AprobadaGuardrail, s.AfirmacionesVerificables, s.AfirmacionesNoSustentadas, s.TextoFinal }
            : null,
        e.IntencionPredicha,
        e.Regeneraciones,
        tiempos = new
        {
            e.T_Ocr_ms,
            e.L_Cls_ms,
            e.T_Guardrail_ms,
            e.T_Rag_ms,
            e.L_Gen_ms,
            e.T_Orq_ms,
            e.L_Total_ms,
        },
        tokens = new { entrada = e.TokensIn, salida = e.TokensOut },
        transiciones = e.Transiciones.OrderBy(t => t.MarcaUtc).ThenBy(t => t.IdTransicion).Select(t => new { t.Estado, t.MarcaUtc }),
        e.InicioUtc,
        e.FinUtc,
    };
}
