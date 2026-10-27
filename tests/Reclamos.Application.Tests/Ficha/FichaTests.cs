using Reclamos.Application.Ficha;
using Reclamos.Domain.Entidades;
using Reclamos.Domain.Enums;

namespace Reclamos.Application.Tests.Ficha;

internal sealed class RelojFalso(DateTime inicio) : TimeProvider
{
    public DateTime Ahora { get; private set; } = inicio;

    public void Avanzar(TimeSpan t) => Ahora += t;

    public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
}

/// <summary>Almacén en memoria para la ficha.</summary>
internal sealed class AlmacenFalso : IAlmacenFicha
{
    public List<AsignacionManual> Asignaciones { get; } = [];
    public List<SesionManual> Sesiones { get; } = [];
    public List<RegistroManual> Registros { get; } = [];
    public int Guardados { get; private set; }

    public Task<IReadOnlyList<AsignacionManual>> AsignacionAsync(string analista, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AsignacionManual>>(Asignaciones.Where(a => a.CodigoAnalista == analista).OrderBy(a => a.Orden).ToList());

    public Task<IReadOnlyList<SesionManual>> SesionesAsync(string analista, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SesionManual>>(Sesiones.Where(s => s.CodigoAnalista == analista).ToList());

    public Task<CasoParaAnalista?> CasoAsync(int idExpediente, CancellationToken ct) =>
        Task.FromResult<CasoParaAnalista?>(new CasoParaAnalista(idExpediente, $"narración {idExpediente}", $"/datos/{idExpediente}.jpg"));

    public void Agregar(SesionManual sesion) => Sesiones.Add(sesion);

    public void Agregar(RegistroManual registro)
    {
        registro.IdRegistro = Registros.Count + 1;
        Registros.Add(registro);
    }

    public Task GuardarAsync(CancellationToken ct)
    {
        Guardados++;
        return Task.CompletedTask;
    }
}

public class CronometroTests
{
    private static readonly DateTime T0 = new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Umbral = TimeSpan.FromSeconds(60);

    private static SesionManual Nueva() => Cronometro.Abrir("A1", 7, T0);

    [Fact]
    public void Sin_pausas_el_tiempo_es_TFinal_menos_TIngreso()
    {
        var s = Nueva();
        Cronometro.Latido(s, T0.AddSeconds(15), Umbral);
        Cronometro.Enviar(s, T0.AddSeconds(30), Umbral);
        Assert.Equal(30_000, Cronometro.TiempoEfectivoMs(s));
        Assert.Equal(0, s.Interrupciones);
    }

    [Fact]
    public void Pausa_y_reanudacion_se_descuentan()
    {
        var s = Nueva();
        Cronometro.Pausar(s, T0.AddSeconds(10), Umbral);
        Cronometro.Reanudar(s, T0.AddSeconds(310)); // 5 min en pausa: no es interrupción
        Cronometro.Enviar(s, T0.AddSeconds(320), Umbral);
        Assert.Equal(300_000, s.PausasMs);
        Assert.Equal(20_000, Cronometro.TiempoEfectivoMs(s));
        Assert.Equal(0, s.Interrupciones);
    }

    [Fact]
    public void Enviar_en_pausa_cierra_la_pausa()
    {
        var s = Nueva();
        Cronometro.Pausar(s, T0.AddSeconds(10), Umbral);
        Cronometro.Enviar(s, T0.AddSeconds(40), Umbral);
        Assert.Null(s.PausaInicioUtc);
        Assert.Equal(10_000, Cronometro.TiempoEfectivoMs(s));
    }

    [Fact]
    public void Pausar_dos_veces_no_reinicia_la_pausa()
    {
        var s = Nueva();
        Cronometro.Pausar(s, T0.AddSeconds(10), Umbral);
        Cronometro.Pausar(s, T0.AddSeconds(20), Umbral);
        Cronometro.Reanudar(s, T0.AddSeconds(30));
        Assert.Equal(20_000, s.PausasMs);
        Cronometro.Reanudar(s, T0.AddSeconds(40)); // reanudar sin pausa no hace nada
        Assert.Equal(20_000, s.PausasMs);
    }

    [Fact]
    public void Hueco_sin_latido_mayor_al_umbral_cuenta_como_pausa_e_interrupcion()
    {
        var s = Nueva();
        Cronometro.Latido(s, T0.AddSeconds(15), Umbral);
        Cronometro.Latido(s, T0.AddSeconds(15 + 600), Umbral); // pestaña cerrada 10 min
        Cronometro.Enviar(s, T0.AddSeconds(15 + 600 + 5), Umbral);
        Assert.Equal(1, s.Interrupciones);
        Assert.Equal(600_000, s.PausasMs);
        Assert.Equal(20_000, Cronometro.TiempoEfectivoMs(s));
    }

    [Fact]
    public void Hueco_igual_al_umbral_no_es_interrupcion()
    {
        var s = Nueva();
        Cronometro.Latido(s, T0.AddSeconds(60), Umbral);
        Assert.Equal((0, 0), (s.Interrupciones, s.PausasMs));
    }

    [Fact]
    public void En_pausa_la_falta_de_latidos_no_se_cuenta_dos_veces()
    {
        var s = Nueva();
        Cronometro.Pausar(s, T0.AddSeconds(10), Umbral);
        Cronometro.Latido(s, T0.AddSeconds(1000), Umbral);
        Cronometro.Reanudar(s, T0.AddSeconds(1010));
        Assert.Equal(0, s.Interrupciones);
        Assert.Equal(1_000_000, s.PausasMs);
    }
}

public class ServicioFichaTests
{
    private static readonly DateTime T0 = new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
    private readonly RelojFalso _reloj = new(T0);
    private readonly AlmacenFalso _almacen = new();
    private readonly ServicioFicha _ficha;

    public ServicioFichaTests()
    {
        _almacen.Asignaciones.AddRange(
        [
            new AsignacionManual { CodigoAnalista = "A1", IdExpediente = 30, Orden = 1, EsCalibracion = false },
            new AsignacionManual { CodigoAnalista = "A1", IdExpediente = 10, Orden = 2, EsCalibracion = true },
            new AsignacionManual { CodigoAnalista = "A1", IdExpediente = 20, Orden = 3, EsCalibracion = false },
            new AsignacionManual { CodigoAnalista = "A2", IdExpediente = 99, Orden = 1, EsCalibracion = false },
        ]);
        var opciones = new FichaOptions
        {
            LatidoSegundos = 15,
            UmbralInactividadSegundos = 60,
            Pines = new() { ["A1"] = "1234", ["A2"] = "5678" },
        };
        _ficha = new ServicioFicha(_almacen, opciones, _reloj);
    }

    private static RespuestaAnalista Respuesta() =>
        new(Intencion.C1, 150.50m, new DateOnly(2026, 3, 10), "AN12345678", Ruta.Procedente, "Respuesta");

    private Task<CasoAbierto> Abrir(int id) => _ficha.AbrirAsync("A1", id, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("A1", "1234", true)]
    [InlineData("A1", "0000", false)]
    [InlineData("A3", "", false)]   // sin PIN configurado no ingresa
    [InlineData("B9", "1234", false)]
    public void Autenticacion_con_codigo_y_pin(string codigo, string pin, bool esperado)
    {
        Assert.Equal(esperado, _ficha.Autenticar(codigo, pin));
    }

    [Fact]
    public async Task La_lista_respeta_el_orden_y_no_revela_la_calibracion()
    {
        var lista = await _ficha.ListarAsync("A1", TestContext.Current.CancellationToken);
        Assert.Equal([30, 10, 20], lista.Select(c => c.IdExpediente));
        Assert.Equal([true, false, false], lista.Select(c => c.Habilitado));
        Assert.All(lista, c => Assert.Equal(EstadoCaso.Pendiente, c.Estado));
    }

    [Fact]
    public async Task Solo_se_abre_el_siguiente_caso_pendiente()
    {
        var ex = await Assert.ThrowsAsync<FichaException>(() => Abrir(10));
        Assert.Equal(ErrorFicha.FueraDeOrden, ex.Error);

        var caso = await Abrir(30);
        Assert.Equal(("narración 30", false), (caso.Narracion, caso.EnPausa));
        Assert.Equal(T0, _almacen.Sesiones.Single().TIngresoUtc);
    }

    [Fact]
    public async Task Caso_no_asignado_no_se_abre()
    {
        var ex = await Assert.ThrowsAsync<FichaException>(() => Abrir(99));
        Assert.Equal(ErrorFicha.NoAsignado, ex.Error);
    }

    [Fact]
    public async Task Flujo_completo_con_pausa_registra_en_RegistroManual()
    {
        var ct = TestContext.Current.CancellationToken;
        await Abrir(30);
        _reloj.Avanzar(TimeSpan.FromSeconds(10));
        await _ficha.LatidoAsync("A1", 30, ct);
        await _ficha.PausarAsync("A1", 30, ct);
        _reloj.Avanzar(TimeSpan.FromSeconds(100));
        await _ficha.ReanudarAsync("A1", 30, ct);
        _reloj.Avanzar(TimeSpan.FromSeconds(20));

        var r = await _ficha.EnviarAsync("A1", 30, Respuesta(), ct);

        var registro = _almacen.Registros.Single();
        Assert.Equal(("A1", 30, false), (registro.CodigoAnalista, registro.IdExpediente, registro.EsCalibracion));
        Assert.Equal((T0, T0.AddSeconds(130), 100_000), (registro.TIngresoUtc, registro.TFinalUtc, registro.PausasMs));
        Assert.Equal((Intencion.C1, Ruta.Procedente, 150.50m), (registro.Intencion!.Value, registro.Ruta!.Value, registro.Monto!.Value));
        Assert.Equal(30_000, r.TiempoEfectivoMs);
        Assert.Equal(registro.IdRegistro, _almacen.Sesiones.Single().IdRegistro);

        var lista = await _ficha.ListarAsync("A1", ct);
        Assert.Equal([EstadoCaso.Enviado, EstadoCaso.Pendiente, EstadoCaso.Pendiente], lista.Select(c => c.Estado));
        Assert.Equal([false, true, false], lista.Select(c => c.Habilitado));
    }

    [Fact]
    public async Task La_calibracion_se_marca_en_el_registro()
    {
        var ct = TestContext.Current.CancellationToken;
        await Abrir(30);
        await _ficha.EnviarAsync("A1", 30, Respuesta(), ct);
        await Abrir(10);
        await _ficha.EnviarAsync("A1", 10, Respuesta(), ct);
        Assert.True(_almacen.Registros.Single(r => r.IdExpediente == 10).EsCalibracion);
    }

    [Fact]
    public async Task Volver_a_abrir_retoma_y_descuenta_la_ausencia()
    {
        var ct = TestContext.Current.CancellationToken;
        await Abrir(30);
        _reloj.Avanzar(TimeSpan.FromMinutes(30)); // cerró la pestaña
        var caso = await Abrir(30);
        Assert.Single(_almacen.Sesiones);
        Assert.Equal(1, _almacen.Sesiones.Single().Interrupciones);
        Assert.Equal(EstadoCaso.EnCurso, (await _ficha.ListarAsync("A1", ct)).First().Estado);
        Assert.False(caso.EnPausa);
    }

    [Fact]
    public async Task Caso_enviado_no_se_vuelve_a_abrir_ni_enviar()
    {
        var ct = TestContext.Current.CancellationToken;
        await Abrir(30);
        await _ficha.EnviarAsync("A1", 30, Respuesta(), ct);
        Assert.Equal(ErrorFicha.YaEnviado, (await Assert.ThrowsAsync<FichaException>(() => Abrir(30))).Error);
        Assert.Equal(ErrorFicha.YaEnviado,
            (await Assert.ThrowsAsync<FichaException>(() => _ficha.EnviarAsync("A1", 30, Respuesta(), ct))).Error);
    }

    [Fact]
    public async Task Eventos_sin_sesion_abierta_fallan()
    {
        var ex = await Assert.ThrowsAsync<FichaException>(() => _ficha.PausarAsync("A1", 30, TestContext.Current.CancellationToken));
        Assert.Equal(ErrorFicha.SinSesion, ex.Error);
    }

    [Fact]
    public async Task Voucher_solo_para_casos_asignados()
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal("/datos/30.jpg", await _ficha.RutaVoucherAsync("A1", 30, ct));
        await Assert.ThrowsAsync<FichaException>(() => _ficha.RutaVoucherAsync("A1", 99, ct));
    }
}
