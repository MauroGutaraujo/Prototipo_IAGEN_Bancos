using Microsoft.EntityFrameworkCore;
using Reclamos.Application.Ficha;
using Reclamos.Domain.Entidades;

namespace Reclamos.Infrastructure.Persistence;

/// <summary>Persistencia de la ficha T0 (app.AsignacionManual, app.SesionManual, app.RegistroManual). Nunca lee eval.</summary>
public sealed class AlmacenFichaEf(ReclamosDbContext db, BancoOptions banco) : IAlmacenFicha
{
    public async Task<IReadOnlyList<AsignacionManual>> AsignacionAsync(string analista, CancellationToken ct) =>
        await db.AsignacionesManuales.AsNoTracking()
            .Where(a => a.CodigoAnalista == analista)
            .OrderBy(a => a.Orden)
            .ToListAsync(ct);

    // Con seguimiento: los eventos (latido, pausa, envío) modifican la sesión y se guardan.
    public async Task<IReadOnlyList<SesionManual>> SesionesAsync(string analista, CancellationToken ct) =>
        await db.SesionesManuales.Where(s => s.CodigoAnalista == analista).ToListAsync(ct);

    public async Task<CasoParaAnalista?> CasoAsync(int idExpediente, CancellationToken ct)
    {
        var e = await db.Expedientes.AsNoTracking()
            .Include(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.IdExpediente == idExpediente, ct);
        if (e is null)
            return null;
        var evidencia = e.Evidencias.OrderBy(v => v.IdEvidencia).First();
        return new CasoParaAnalista(e.IdExpediente, e.NarracionCliente,
            Path.Combine(Rutas.ResolverDirectorio(banco.DirectorioDatos), evidencia.RutaImagen));
    }

    public void Agregar(SesionManual sesion) => db.SesionesManuales.Add(sesion);

    public void Agregar(RegistroManual registro) => db.RegistrosManuales.Add(registro);

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
