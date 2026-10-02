using Moq;
using Reclamos.Application.Agentes;
using Reclamos.Application.Llm;
using Reclamos.Application.Orquestacion;
using Reclamos.Application.Rag;
using Reclamos.Domain.Entidades;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using Reclamos.Guardrails.Salida;
using static Reclamos.Domain.Enums.EstadoExpediente;

namespace Reclamos.Application.Tests.Orquestacion;

public class OrquestadorTests
{
    private static readonly TransaccionCore Tx = new(
        "AN12345678", 1, 150.5m, Moneda.PEN, new DateTime(2026, 3, 10, 12, 0, 0), "Comercio X",
        EstadoTransaccion.Completada, true, "DEV-1", false);

    private static readonly ExpedienteParaProcesar Expediente =
        new(7, "me cobraron dos veces", "/datos/vouchers/0007.jpg", new HechosCore(Tx, [Tx], null, "DEV-1"));

    private static readonly FragmentoRecuperado Fragmento = new("sbs-04036-2022-art-7", 0.5, "Norma", "7", "texto");

    private readonly Mock<IFuenteExpedientes> _fuente = new();
    private readonly Mock<IOcrAgent> _ocr = new();
    private readonly Mock<IIntentClassifier> _clasificador = new();
    private readonly Mock<ILegalAgent> _legal = new();
    private readonly Mock<INormativeRetriever> _rag = new();
    private readonly Mock<IResolutionGenerator> _generador = new();
    private readonly Mock<IOutputGuardrail> _guardrail = new();
    private readonly Mock<IAlmacenEjecuciones> _almacen = new();
    private Ejecucion? _guardada;

    public OrquestadorTests()
    {
        _fuente.Setup(f => f.ObtenerAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(Expediente);
        _ocr.Setup(o => o.ExtraerAsync(Expediente.RutaVoucher, It.IsAny<CancellationToken>())).ReturnsAsync(
            new LecturaOcr(new ResultadoOcr(150.5m, Moneda.PEN, Tx.FechaHora, Tx.Codigo, 0.95m, 0.96m, 0.97m), "tsv", null,
                new MotorOcr("tesseract 5", "sha")));
        _clasificador.Setup(c => c.ClasificarAsync(Expediente.Narracion, "m1", It.IsAny<CancellationToken>())).ReturnsAsync(
            new ResultadoClasificacion(new SalidaClasificador(true, Intencion.C1, 150.5m, Moneda.PEN, null, null), 1, "m1-v", 10, 3));
        Decide(new DictamenLegal(Ruta.Procedente, "R5", "CARGO_DUPLICADO"));
        Recupera(CalificacionRecuperacion.Correcta, Fragmento);
        _generador.Setup(g => g.GenerarAsync(It.IsAny<SolicitudResolucion>(), "m1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoGeneracion("borrador 1", [], "m1-v", 100, 50));
        _generador.Setup(g => g.RegenerarAsync(It.IsAny<ResultadoGeneracion>(), It.IsAny<IReadOnlyList<string>>(), "m1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoGeneracion("borrador 2", [], "m1-v", 200, 60));
        Verifica(Aprobado());
        _almacen.Setup(a => a.GuardarAsync(It.IsAny<Ejecucion>(), It.IsAny<CancellationToken>()))
            .Callback<Ejecucion, CancellationToken>((e, _) => _guardada = e)
            .ReturnsAsync(42L);
    }

    private void Decide(DictamenLegal d) => _legal.Setup(l => l.Decidir(It.IsAny<EntradaLegal>())).Returns(d);

    private void Recupera(CalificacionRecuperacion c, params FragmentoRecuperado[] admitidos) =>
        _rag.Setup(r => r.RecuperarAsync(It.IsAny<ConsultaNormativa>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoRecuperacion(c, admitidos,
                admitidos.Select(f => new FragmentoEvaluado(f.Id, f.Score, true, 1)).ToList(), "consulta.v1", TimeSpan.Zero));

    private void Verifica(params VeredictoGuardrail[] veredictos)
    {
        var cola = new Queue<VeredictoGuardrail>(veredictos);
        _guardrail.Setup(g => g.Verificar(It.IsAny<EntradaGuardrailSalida>()))
            .Returns<EntradaGuardrailSalida>(e => cola.Count > 1 ? cola.Dequeue() : cola.Peek() with { ModoAblacion = e.ModoAblacion });
    }

    private static VeredictoGuardrail Aprobado() => new([], 4, 0, false);

    private static VeredictoGuardrail ConFallas() =>
        new([new FallaGuardrail(TipoFalla.FaltaPlazo, "plazo"), new FallaGuardrail(TipoFalla.CitaNoAdmitida, "x")], 5, 1, false);

    private OrquestadorExpedientes Orquestador() => new(
        _fuente.Object, _ocr.Object, _clasificador.Object, _legal.Object, _rag.Object, _generador.Object,
        _guardrail.Object, _almacen.Object, TimeProvider.System);

    private Task<ResultadoProcesamiento> Procesar(Condicion condicion = Condicion.T1) =>
        Orquestador().ProcesarAsync(new SolicitudProcesamiento(7, "m1", condicion, 1), TestContext.Current.CancellationToken);

    private EstadoExpediente[] Estados() => _guardada!.Transiciones.Select(t => t.Estado).ToArray();

    [Fact]
    public async Task Camino_completo_emite_y_registra_todo()
    {
        var r = await Procesar();

        Assert.Equal(42L, r.IdEjecucion);
        Assert.Null(r.Error);
        Assert.Equal([Recibido, OcrExtraido, Clasificado, Decidido, Fundamentado, Redactado, Verificado, Emitido], Estados());
        var e = _guardada!;
        Assert.Equal(Emitido, e.EstadoFinal);
        Assert.Null(e.MotivoDerivacion);
        Assert.Equal((150.5m, 0.95m, 0.96m, 0.97m), (e.MontoExtraido!.Value, e.ConfMonto!.Value, e.ConfFecha!.Value, e.ConfCodigo!.Value));
        Assert.Equal(Intencion.C1, e.IntencionPredicha);
        Assert.Equal(("R5", Ruta.Procedente), (e.DecisionLegal!.Regla, e.DecisionLegal.Ruta));
        Assert.Equal(CalificacionRecuperacion.Correcta, e.Recuperacion!.Calificacion);
        Assert.Equal(("borrador 1", "borrador 1", true), (e.Resolucion!.TextoBorrador, e.Resolucion.TextoFinal, e.Resolucion.AprobadaGuardrail));
        Assert.Equal((4, 0), (e.Resolucion.AfirmacionesVerificables!.Value, e.Resolucion.AfirmacionesNoSustentadas!.Value));
        Assert.Equal((110, 53), (e.TokensIn!.Value, e.TokensOut!.Value));
        Assert.Equal(("m1", "m1-v", (byte)1, Condicion.T1), (e.ModeloId, e.ModeloVersion, e.Repeticion, e.Condicion));
        Assert.Equal(OrquestadorExpedientes.VersionPrompts, e.PromptVersion);
        Assert.Equal(0, e.Regeneraciones);
        Assert.True(e.FinUtc >= e.InicioUtc);
    }

    [Fact]
    public async Task Los_tiempos_cumplen_la_descomposicion_aditiva()
    {
        await Procesar();
        var e = _guardada!;
        var etapas = new[] { e.T_Ocr_ms, e.L_Cls_ms, e.T_Guardrail_ms, e.T_Rag_ms, e.L_Gen_ms, e.T_Orq_ms };
        Assert.All(etapas, t => Assert.True(t >= 0));
        Assert.InRange(e.L_Total_ms - etapas.Sum(), -6, 6); // solo el redondeo a ms por etapa
    }

    [Fact]
    public async Task Derivacion_del_agente_legal_no_recupera_ni_redacta()
    {
        Decide(new DictamenLegal(Ruta.Derivar, "R3", "MONTO_SUPERA_UMBRAL"));

        await Procesar();

        Assert.Equal([Recibido, OcrExtraido, Clasificado, Decidido, Derivado], Estados());
        Assert.Equal("R3", _guardada!.MotivoDerivacion);
        Assert.Null(_guardada.Recuperacion);
        Assert.Null(_guardada.Resolucion);
        _rag.VerifyNoOtherCalls();
        _generador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Crag_incorrecta_deriva_sin_redactar()
    {
        Recupera(CalificacionRecuperacion.Incorrecta);

        await Procesar();

        Assert.Equal([Recibido, OcrExtraido, Clasificado, Decidido, Derivado], Estados());
        Assert.Equal("CRAG_INCORRECTA", _guardada!.MotivoDerivacion);
        Assert.Equal(CalificacionRecuperacion.Incorrecta, _guardada.Recuperacion!.Calificacion);
        _generador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Una_regeneracion_que_pasa_emite_el_segundo_borrador()
    {
        Verifica(ConFallas(), Aprobado());

        await Procesar();

        Assert.Equal([Recibido, OcrExtraido, Clasificado, Decidido, Fundamentado, Redactado, Verificado, Redactado, Verificado, Emitido],
            Estados());
        var e = _guardada!;
        Assert.Equal(1, e.Regeneraciones);
        Assert.Equal(("borrador 1", "borrador 2", true), (e.Resolucion!.TextoBorrador, e.Resolucion.TextoFinal, e.Resolucion.AprobadaGuardrail));
        Assert.Equal((5, 1), (e.Resolucion.AfirmacionesVerificables!.Value, e.Resolucion.AfirmacionesNoSustentadas!.Value)); // TA: 1.er borrador
        Assert.Equal((310, 113), (e.TokensIn!.Value, e.TokensOut!.Value));
        _generador.Verify(g => g.RegenerarAsync(It.IsAny<ResultadoGeneracion>(),
            It.Is<IReadOnlyList<string>>(f => f.Count == 2), "m1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dos_borradores_rechazados_derivan_por_guardrail()
    {
        Verifica(ConFallas(), ConFallas());

        await Procesar();

        Assert.Equal(Derivado, _guardada!.EstadoFinal);
        Assert.Equal("GUARDRAIL_SALIDA", _guardada.MotivoDerivacion);
        Assert.Null(_guardada.Resolucion!.TextoFinal);
        Assert.False(_guardada.Resolucion.AprobadaGuardrail);
        _generador.Verify(g => g.RegenerarAsync(It.IsAny<ResultadoGeneracion>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ablacion_T2_no_recupera_y_el_guardrail_no_bloquea()
    {
        Recupera(CalificacionRecuperacion.Omitida);
        Verifica(ConFallas());

        await Procesar(Condicion.T2);

        _rag.Verify(r => r.RecuperarAsync(It.IsAny<ConsultaNormativa>(), true, It.IsAny<CancellationToken>()), Times.Once);
        _guardrail.Verify(g => g.Verificar(It.Is<EntradaGuardrailSalida>(e => e.ModoAblacion && e.FragmentosAdmitidos.Count == 0)), Times.Once);
        var e = _guardada!;
        Assert.Equal(Emitido, e.EstadoFinal);
        Assert.Equal(0, e.Regeneraciones);
        Assert.False(e.Resolucion!.AprobadaGuardrail); // se registra el veredicto para TA
        Assert.Equal(Condicion.T2, e.Condicion);
    }

    [Fact]
    public async Task Excepcion_en_una_etapa_deja_estado_error_y_se_registra()
    {
        _clasificador.Setup(c => c.ClasificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429 Too Many Requests"));

        var r = await Procesar();

        Assert.Equal([Recibido, OcrExtraido, EstadoExpediente.Error], Estados());
        Assert.Equal("ERROR", _guardada!.MotivoDerivacion);
        Assert.Contains("429", r.Error);
        _almacen.Verify(a => a.GuardarAsync(It.IsAny<Ejecucion>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Clasificador_invalido_llega_al_agente_legal_para_R4()
    {
        _clasificador.Setup(c => c.ClasificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoClasificacion(new SalidaClasificador(false, null, null, null, null, null), 2, "m1-v", 20, 6));
        Decide(new DictamenLegal(Ruta.Derivar, "R4", "CLASIFICADOR_INVALIDO"));

        await Procesar();

        _legal.Verify(l => l.Decidir(It.Is<EntradaLegal>(e => !e.Clasificador.EsValida)), Times.Once);
        Assert.Null(_guardada!.IntencionPredicha);
        Assert.Equal("R4", _guardada.MotivoDerivacion);
    }

    [Fact]
    public async Task Expediente_inexistente_no_registra_nada()
    {
        await Assert.ThrowsAsync<ExpedienteNoEncontradoException>(() =>
            Orquestador().ProcesarAsync(new SolicitudProcesamiento(999, "m1", Condicion.T1, 1), TestContext.Current.CancellationToken));
        _almacen.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task La_condicion_T0_es_manual()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Procesar(Condicion.T0));
    }

    [Fact]
    public void Las_fallas_del_guardrail_se_describen_para_el_mensaje_correctivo()
    {
        Assert.Contains("[F:x]", OrquestadorExpedientes.Describir(new FallaGuardrail(TipoFalla.CitaNoAdmitida, "x")));
        Assert.All(Enum.GetValues<TipoFalla>(), t =>
            Assert.False(string.IsNullOrWhiteSpace(OrquestadorExpedientes.Describir(new FallaGuardrail(t, "d")))));
    }
}
