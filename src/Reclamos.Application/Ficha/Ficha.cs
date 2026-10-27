using System.Security.Cryptography;
using System.Text;
using Reclamos.Domain.Entidades;
using Reclamos.Domain.Enums;

namespace Reclamos.Application.Ficha;

/// <summary>Parámetros de la ficha de cronometraje T0 (sección <c>Ficha</c>, SPEC §5).</summary>
public sealed class FichaOptions
{
    public const string Seccion = "Ficha";

    /// <summary>Cada cuánto envía el navegador un latido mientras el caso está abierto y sin pausa.</summary>
    public int LatidoSegundos { get; set; } = 15;

    /// <summary>Un hueco sin latido mayor a este umbral cuenta como pausa (interrupción).</summary>
    public int UmbralInactividadSegundos { get; set; } = 60;

    /// <summary>PIN por analista (A1–A3). Solo por variables de entorno / secrets: Ficha__Pines__A1…</summary>
    public Dictionary<string, string> Pines { get; set; } = [];
}

/// <summary>Reglas de tiempo (servidor, UTC). Tiempo manual = TFinal − TIngreso − PausasMs.</summary>
public static class Cronometro
{
    public static SesionManual Abrir(string analista, int idExpediente, DateTime ahora) => new()
    {
        CodigoAnalista = analista,
        IdExpediente = idExpediente,
        TIngresoUtc = ahora,
        UltimoLatidoUtc = ahora,
    };

    /// <summary>Latido o regreso: un hueco sin latido mayor al umbral (sin pausa en curso) se suma como pausa.</summary>
    public static void Latido(SesionManual s, DateTime ahora, TimeSpan umbral)
    {
        var hueco = ahora - s.UltimoLatidoUtc;
        if (s.PausaInicioUtc is null && hueco > umbral)
        {
            s.PausasMs += Ms(hueco);
            s.Interrupciones++;
        }
        s.UltimoLatidoUtc = ahora;
    }

    public static void Pausar(SesionManual s, DateTime ahora, TimeSpan umbral)
    {
        Latido(s, ahora, umbral);
        s.PausaInicioUtc ??= ahora;
    }

    public static void Reanudar(SesionManual s, DateTime ahora)
    {
        if (s.PausaInicioUtc is { } inicio)
        {
            s.PausasMs += Ms(ahora - inicio);
            s.PausaInicioUtc = null;
        }
        s.UltimoLatidoUtc = ahora;
    }

    public static void Enviar(SesionManual s, DateTime ahora, TimeSpan umbral)
    {
        if (s.PausaInicioUtc is not null)
            Reanudar(s, ahora);
        else
            Latido(s, ahora, umbral);
        s.TFinalUtc = ahora;
    }

    public static int TiempoEfectivoMs(SesionManual s) =>
        Ms((s.TFinalUtc ?? s.UltimoLatidoUtc) - s.TIngresoUtc) - s.PausasMs;

    private static int Ms(TimeSpan t) => (int)Math.Round(t.TotalMilliseconds, MidpointRounding.AwayFromZero);
}

/// <summary>Narración y voucher del expediente (sin salida del sistema ni verdad de referencia).</summary>
public sealed record CasoParaAnalista(int IdExpediente, string Narracion, string RutaVoucher);

/// <summary>Persistencia de la ficha (core/app; nunca eval).</summary>
public interface IAlmacenFicha
{
    Task<IReadOnlyList<AsignacionManual>> AsignacionAsync(string analista, CancellationToken ct);
    Task<IReadOnlyList<SesionManual>> SesionesAsync(string analista, CancellationToken ct);
    Task<CasoParaAnalista?> CasoAsync(int idExpediente, CancellationToken ct);
    void Agregar(SesionManual sesion);
    void Agregar(RegistroManual registro);
    Task GuardarAsync(CancellationToken ct);
}

public enum EstadoCaso
{
    Pendiente,
    EnCurso,
    Enviado,
}

/// <summary>Un caso en la lista del analista (no revela si es de calibración).</summary>
public sealed record CasoEnLista(int Orden, int IdExpediente, EstadoCaso Estado, bool Habilitado);

public sealed record CasoAbierto(int IdExpediente, string Narracion, bool EnPausa, int LatidoSegundos);

public sealed record RespuestaAnalista(
    Intencion? Intencion, decimal? Monto, DateOnly? Fecha, string? Codigo, Ruta? Ruta, string? TextoRespuesta);

public sealed record CasoEnviado(int IdRegistro, int TiempoEfectivoMs, int PausasMs, int Interrupciones);

public enum ErrorFicha
{
    NoAsignado,
    FueraDeOrden,
    YaEnviado,
    SinSesion,
}

public sealed class FichaException(ErrorFicha error, string mensaje) : Exception(mensaje)
{
    public ErrorFicha Error { get; } = error;
}

/// <summary>Ficha de cronometraje T0 (SPEC §5): orden obligatorio, tiempos del servidor y registro al enviar.</summary>
public sealed class ServicioFicha(IAlmacenFicha almacen, FichaOptions opciones, TimeProvider reloj)
{
    private TimeSpan Umbral => TimeSpan.FromSeconds(opciones.UmbralInactividadSegundos);

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    /// <summary>Código A1–A3 con su PIN configurado; comparación en tiempo constante.</summary>
    public bool Autenticar(string codigo, string pin)
    {
        if (!opciones.Pines.TryGetValue(codigo, out var esperado) || string.IsNullOrEmpty(esperado))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pin), Encoding.UTF8.GetBytes(esperado));
    }

    public async Task<IReadOnlyList<CasoEnLista>> ListarAsync(string analista, CancellationToken ct)
    {
        var (asignacion, sesiones) = (await almacen.AsignacionAsync(analista, ct), await almacen.SesionesAsync(analista, ct));
        var siguiente = Siguiente(asignacion, sesiones);
        return asignacion.Select(a =>
        {
            var s = sesiones.FirstOrDefault(x => x.IdExpediente == a.IdExpediente);
            var estado = s is null ? EstadoCaso.Pendiente : s.TFinalUtc is null ? EstadoCaso.EnCurso : EstadoCaso.Enviado;
            return new CasoEnLista(a.Orden, a.IdExpediente, estado, a.IdExpediente == siguiente);
        }).ToList();
    }

    /// <summary>Abre el siguiente caso pendiente o retoma el que quedó abierto (el hueco cuenta como pausa).</summary>
    public async Task<CasoAbierto> AbrirAsync(string analista, int idExpediente, CancellationToken ct)
    {
        var asignacion = await almacen.AsignacionAsync(analista, ct);
        if (asignacion.All(a => a.IdExpediente != idExpediente))
            throw new FichaException(ErrorFicha.NoAsignado, $"El expediente {idExpediente} no está asignado a {analista}");

        var sesiones = await almacen.SesionesAsync(analista, ct);
        var sesion = sesiones.FirstOrDefault(s => s.IdExpediente == idExpediente);
        if (sesion?.TFinalUtc is not null)
            throw new FichaException(ErrorFicha.YaEnviado, $"El expediente {idExpediente} ya fue enviado");
        if (sesion is null)
        {
            if (Siguiente(asignacion, sesiones) != idExpediente)
                throw new FichaException(ErrorFicha.FueraDeOrden, "Solo se puede abrir el siguiente caso de la lista");
            sesion = Cronometro.Abrir(analista, idExpediente, Ahora);
            almacen.Agregar(sesion);
        }
        else
        {
            Cronometro.Latido(sesion, Ahora, Umbral);
        }
        await almacen.GuardarAsync(ct);

        var caso = await almacen.CasoAsync(idExpediente, ct)
            ?? throw new FichaException(ErrorFicha.NoAsignado, $"No existe el expediente {idExpediente}");
        return new CasoAbierto(caso.IdExpediente, caso.Narracion, sesion.PausaInicioUtc is not null, opciones.LatidoSegundos);
    }

    public Task LatidoAsync(string analista, int idExpediente, CancellationToken ct) =>
        EventoAsync(analista, idExpediente, s => Cronometro.Latido(s, Ahora, Umbral), ct);

    public Task PausarAsync(string analista, int idExpediente, CancellationToken ct) =>
        EventoAsync(analista, idExpediente, s => Cronometro.Pausar(s, Ahora, Umbral), ct);

    public Task ReanudarAsync(string analista, int idExpediente, CancellationToken ct) =>
        EventoAsync(analista, idExpediente, s => Cronometro.Reanudar(s, Ahora), ct);

    public async Task<CasoEnviado> EnviarAsync(string analista, int idExpediente, RespuestaAnalista r, CancellationToken ct)
    {
        var sesion = await SesionAbiertaAsync(analista, idExpediente, ct);
        var asignado = (await almacen.AsignacionAsync(analista, ct)).First(a => a.IdExpediente == idExpediente);
        Cronometro.Enviar(sesion, Ahora, Umbral);

        var registro = new RegistroManual
        {
            IdExpediente = idExpediente,
            CodigoAnalista = analista,
            EsCalibracion = asignado.EsCalibracion,
            TIngresoUtc = sesion.TIngresoUtc,
            TFinalUtc = sesion.TFinalUtc!.Value,
            PausasMs = sesion.PausasMs,
            Intencion = r.Intencion,
            Monto = r.Monto,
            Fecha = r.Fecha,
            Codigo = r.Codigo,
            Ruta = r.Ruta,
            TextoRespuesta = r.TextoRespuesta,
        };
        almacen.Agregar(registro);
        await almacen.GuardarAsync(ct);
        sesion.IdRegistro = registro.IdRegistro;
        await almacen.GuardarAsync(ct);
        return new CasoEnviado(registro.IdRegistro, Cronometro.TiempoEfectivoMs(sesion), sesion.PausasMs, sesion.Interrupciones);
    }

    public async Task<string> RutaVoucherAsync(string analista, int idExpediente, CancellationToken ct)
    {
        if ((await almacen.AsignacionAsync(analista, ct)).All(a => a.IdExpediente != idExpediente))
            throw new FichaException(ErrorFicha.NoAsignado, $"El expediente {idExpediente} no está asignado a {analista}");
        return (await almacen.CasoAsync(idExpediente, ct))?.RutaVoucher
            ?? throw new FichaException(ErrorFicha.NoAsignado, $"No existe el expediente {idExpediente}");
    }

    private async Task EventoAsync(string analista, int idExpediente, Action<SesionManual> evento, CancellationToken ct)
    {
        evento(await SesionAbiertaAsync(analista, idExpediente, ct));
        await almacen.GuardarAsync(ct);
    }

    private async Task<SesionManual> SesionAbiertaAsync(string analista, int idExpediente, CancellationToken ct)
    {
        var sesion = (await almacen.SesionesAsync(analista, ct)).FirstOrDefault(s => s.IdExpediente == idExpediente);
        return sesion switch
        {
            null => throw new FichaException(ErrorFicha.SinSesion, $"El expediente {idExpediente} no está abierto"),
            { TFinalUtc: not null } => throw new FichaException(ErrorFicha.YaEnviado, $"El expediente {idExpediente} ya fue enviado"),
            _ => sesion,
        };
    }

    /// <summary>Primer caso (por orden) que no tiene sesión enviada.</summary>
    private static int? Siguiente(IReadOnlyList<AsignacionManual> asignacion, IReadOnlyList<SesionManual> sesiones) =>
        asignacion.OrderBy(a => a.Orden)
            .FirstOrDefault(a => sesiones.All(s => s.IdExpediente != a.IdExpediente || s.TFinalUtc is null))
            ?.IdExpediente;
}
