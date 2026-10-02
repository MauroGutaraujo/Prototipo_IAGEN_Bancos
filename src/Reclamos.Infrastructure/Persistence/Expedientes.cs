using Microsoft.EntityFrameworkCore;
using Reclamos.Application.Orquestacion;
using Reclamos.Domain.Entidades;
using Reclamos.Guardrails.Legal;
using Reclamos.Infrastructure.Rag;

namespace Reclamos.Infrastructure.Persistence;

/// <summary>Ubicación del banco sintético (vouchers) en el entorno de ejecución.</summary>
public sealed class BancoOptions
{
    public const string Seccion = "Banco";

    /// <summary>Carpeta que contiene vouchers/ (salida de tools/dataset-generator).</summary>
    public string DirectorioDatos { get; set; } = "tools/dataset-generator/data";
}

/// <summary>Carga los hechos del expediente desde core y app. Nunca lee el esquema eval.</summary>
public sealed class FuenteExpedientesEf(ReclamosDbContext db, BancoOptions banco) : IFuenteExpedientes
{
    public async Task<ExpedienteParaProcesar?> ObtenerAsync(int idExpediente, CancellationToken ct)
    {
        var expediente = await db.Expedientes.AsNoTracking()
            .Include(e => e.Transaccion)
            .Include(e => e.Evidencias)
            .FirstOrDefaultAsync(e => e.IdExpediente == idExpediente, ct);
        if (expediente?.Transaccion is not { } tx)
            return null;

        var cliente = await db.Clientes.AsNoTracking().FirstAsync(c => c.IdCliente == tx.IdCliente, ct);
        var movimientos = await db.Transacciones.AsNoTracking().Where(t => t.IdCliente == tx.IdCliente).ToListAsync(ct);
        var dia = DateOnly.FromDateTime(tx.FechaHora);
        var tipoCambio = await db.TiposCambio.AsNoTracking().Where(t => t.Fecha == dia).Select(t => (decimal?)t.UsdPen)
            .FirstOrDefaultAsync(ct);

        var evidencia = expediente.Evidencias.OrderBy(e => e.IdEvidencia).FirstOrDefault()
            ?? throw new InvalidOperationException($"El expediente {idExpediente} no tiene evidencia");
        var directorio = Rutas.ResolverDirectorio(banco.DirectorioDatos);

        return new ExpedienteParaProcesar(
            expediente.IdExpediente,
            expediente.NarracionCliente,
            Path.Combine(directorio, evidencia.RutaImagen),
            new HechosCore(Core(tx), movimientos.Select(Core).ToList(), tipoCambio, cliente.DispositivoRegistrado));
    }

    private static TransaccionCore Core(Transaccion t) => new(
        t.CodigoOperacion, t.IdCliente, t.Monto, t.Moneda, t.FechaHora, t.Comercio, t.Estado,
        t.AutenticacionReforzada, t.Dispositivo, t.IndicadorRiesgo);
}

/// <summary>Guarda la ejecución con sus filas de auditoría en una sola transacción.</summary>
public sealed class AlmacenEjecucionesEf(ReclamosDbContext db) : IAlmacenEjecuciones
{
    public async Task<long> GuardarAsync(Ejecucion ejecucion, CancellationToken ct)
    {
        db.Ejecuciones.Add(ejecucion);
        await db.SaveChangesAsync(ct);
        return ejecucion.IdEjecucion;
    }
}

/// <summary>Rutas relativas se buscan subiendo desde el directorio actual y el de la aplicación.</summary>
public static class Rutas
{
    public static string ResolverDirectorio(string ruta)
    {
        if (Path.IsPathRooted(ruta))
            return ruta;
        foreach (var inicio in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(inicio); dir is not null; dir = dir.Parent)
            {
                var candidata = Path.Combine(dir.FullName, ruta);
                if (Directory.Exists(candidata))
                    return candidata;
            }
        }
        throw new DirectoryNotFoundException($"No se encontró el directorio {ruta}");
    }

    public static string ResolverArchivo(string ruta) => CorpusNormativo.ResolverRuta(ruta);
}
