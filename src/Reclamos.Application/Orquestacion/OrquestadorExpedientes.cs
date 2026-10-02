using System.Diagnostics;
using Reclamos.Application.Agentes;
using Reclamos.Application.Rag;
using Reclamos.Domain.Entidades;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;
using Reclamos.Guardrails.Salida;

namespace Reclamos.Application.Orquestacion;

/// <summary>Hechos del expediente desde los esquemas core y app (nunca eval).</summary>
public sealed record ExpedienteParaProcesar(int Id, string Narracion, string RutaVoucher, HechosCore Core);

public interface IFuenteExpedientes
{
    Task<ExpedienteParaProcesar?> ObtenerAsync(int idExpediente, CancellationToken ct);
}

/// <summary>Persiste la ejecución con sus transiciones, decisión, recuperación y resolución; devuelve el id.</summary>
public interface IAlmacenEjecuciones
{
    Task<long> GuardarAsync(Ejecucion ejecucion, CancellationToken ct);
}

public sealed record SolicitudProcesamiento(int IdExpediente, string ModeloId, Condicion Condicion, byte Repeticion, int? IdCorrida = null);

/// <param name="Error">Mensaje de la excepción si la ejecución terminó en estado Error.</param>
public sealed record ResultadoProcesamiento(long IdEjecucion, Ejecucion Ejecucion, string? Error);

public sealed class ExpedienteNoEncontradoException(int idExpediente)
    : Exception($"No existe el expediente {idExpediente}");

/// <summary>
/// Orquestador secuencial (SPEC §1): OCR → clasificación → Agente Legal → CRAG → generación → guardrail de salida.
/// Mide cada etapa con Stopwatch (descomposición aditiva) y persiste la auditoría al final, fuera de los tiempos.
/// </summary>
public sealed class OrquestadorExpedientes(
    IFuenteExpedientes fuente,
    IOcrAgent ocr,
    IIntentClassifier clasificador,
    ILegalAgent legal,
    INormativeRetriever rag,
    IResolutionGenerator generador,
    IOutputGuardrail guardrail,
    IAlmacenEjecuciones almacen,
    TimeProvider reloj)
{
    public const string VersionPrompts = "clasificador.v1+generador.v2+regeneracion.v1+consulta.v1";

    public async Task<ResultadoProcesamiento> ProcesarAsync(SolicitudProcesamiento s, CancellationToken ct)
    {
        if (s.Condicion == Condicion.T0)
            throw new ArgumentException("T0 es la línea base manual: no pasa por el orquestador.", nameof(s));

        var inicio = Stopwatch.GetTimestamp();
        var expediente = await fuente.ObtenerAsync(s.IdExpediente, ct) ?? throw new ExpedienteNoEncontradoException(s.IdExpediente);

        var ej = new Ejecucion
        {
            IdExpediente = s.IdExpediente,
            IdCorrida = s.IdCorrida,
            Condicion = s.Condicion,
            ModeloId = s.ModeloId,
            PromptVersion = VersionPrompts,
            Repeticion = s.Repeticion,
            InicioUtc = reloj.GetUtcNow().UtcDateTime,
        };
        double tOcr = 0, lCls = 0, tGuardrail = 0, tRag = 0, lGen = 0;
        int tokensIn = 0, tokensOut = 0;
        string? error = null;

        void Transicion(EstadoExpediente estado)
        {
            ej.Transiciones.Add(new TransicionEstado { Estado = estado, MarcaUtc = reloj.GetUtcNow().UtcDateTime });
            ej.EstadoFinal = estado;
        }

        void Derivar(string motivo)
        {
            ej.MotivoDerivacion = motivo;
            Transicion(EstadoExpediente.Derivado);
        }

        Transicion(EstadoExpediente.Recibido);
        try
        {
            await EtapasAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            ej.MotivoDerivacion = "ERROR";
            Transicion(EstadoExpediente.Error);
        }

        // Persistencia una sola vez, fuera de los tiempos medidos.
        var total = Ms(inicio);
        ej.FinUtc = reloj.GetUtcNow().UtcDateTime;
        ej.T_Ocr_ms = Redondear(tOcr);
        ej.L_Cls_ms = Redondear(lCls);
        ej.T_Guardrail_ms = Redondear(tGuardrail);
        ej.T_Rag_ms = Redondear(tRag);
        ej.L_Gen_ms = Redondear(lGen);
        ej.L_Total_ms = Redondear(total);
        ej.T_Orq_ms = Redondear(Math.Max(0, total - (tOcr + lCls + tGuardrail + tRag + lGen)));
        (ej.TokensIn, ej.TokensOut) = (tokensIn, tokensOut);
        var id = await almacen.GuardarAsync(ej, ct);
        return new ResultadoProcesamiento(id, ej, error);

        async Task EtapasAsync()
        {
            // 1. OCR
            var t = Stopwatch.GetTimestamp();
            var lectura = await ocr.ExtraerAsync(expediente.RutaVoucher, ct);
            tOcr += Ms(t);
            var r = lectura.Resultado;
            (ej.MontoExtraido, ej.FechaExtraida, ej.CodigoExtraido) = (r.Monto, r.FechaHora, r.Codigo);
            (ej.ConfMonto, ej.ConfFecha, ej.ConfCodigo) = (r.ConfMonto, r.ConfFecha, r.ConfCodigo);
            Transicion(EstadoExpediente.OcrExtraido);

            // 2. Clasificación
            t = Stopwatch.GetTimestamp();
            var cls = await clasificador.ClasificarAsync(expediente.Narracion, s.ModeloId, ct);
            lCls += Ms(t);
            ej.IntencionPredicha = cls.Salida.EsValida ? cls.Salida.Intencion : null;
            ej.ModeloVersion ??= cls.ModeloVersion;
            (tokensIn, tokensOut) = (tokensIn + cls.TokensIn, tokensOut + cls.TokensOut);
            Transicion(EstadoExpediente.Clasificado);

            // 3. Agente Legal (simbólico): decide la ruta
            t = Stopwatch.GetTimestamp();
            var dictamen = legal.Decidir(new EntradaLegal(r, cls.Salida, expediente.Core));
            tGuardrail += Ms(t);
            ej.DecisionLegal = new DecisionLegal { Ruta = dictamen.Ruta, Regla = dictamen.Regla, Motivo = dictamen.Motivo };
            Transicion(EstadoExpediente.Decidido);
            if (dictamen.Ruta == Ruta.Derivar)
            {
                Derivar(dictamen.Regla);
                return;
            }

            // 4. CRAG (en T2 no consulta Pinecone)
            var ablacion = s.Condicion == Condicion.T2;
            var intencion = cls.Salida.Intencion!.Value; // R4 garantiza intención válida en rutas automáticas
            t = Stopwatch.GetTimestamp();
            var recuperacion = await rag.RecuperarAsync(new ConsultaNormativa(intencion, dictamen.Ruta, dictamen.Regla), ablacion, ct);
            tRag += Ms(t);
            ej.Recuperacion = new Recuperacion { Calificacion = recuperacion.Calificacion, FragmentosJson = recuperacion.FragmentosJson };
            if (recuperacion.Derivar)
            {
                Derivar(recuperacion.MotivoDerivacion!);
                return;
            }
            Transicion(EstadoExpediente.Fundamentado);

            // 5. Generación
            var tx = expediente.Core.Transaccion;
            var solicitud = new SolicitudResolucion(expediente.Id, dictamen.Ruta, dictamen.Regla, dictamen.Motivo, intencion, tx,
                recuperacion.Admitidos);
            t = Stopwatch.GetTimestamp();
            var borrador = await generador.GenerarAsync(solicitud, s.ModeloId, ct);
            lGen += Ms(t);
            ej.ModeloVersion ??= borrador.ModeloVersion;
            (tokensIn, tokensOut) = (tokensIn + borrador.TokensIn, tokensOut + borrador.TokensOut);
            Transicion(EstadoExpediente.Redactado);

            // 6. Guardrail de salida (TA se registra sobre el primer borrador)
            var hechos = new HechosVerificados(expediente.Id, tx.Monto, tx.Moneda, tx.FechaHora, tx.Codigo);
            var admitidos = recuperacion.Admitidos.Select(f => f.Id).ToList();
            VeredictoGuardrail Verificar(string texto)
            {
                var tv = Stopwatch.GetTimestamp();
                var v = guardrail.Verificar(new EntradaGuardrailSalida(texto, dictamen.Ruta, hechos, admitidos, ablacion));
                tGuardrail += Ms(tv);
                Transicion(EstadoExpediente.Verificado);
                return v;
            }

            var veredicto = Verificar(borrador.Borrador);
            var resolucion = new Resolucion
            {
                TextoBorrador = borrador.Borrador,
                AfirmacionesVerificables = veredicto.AfirmacionesVerificables,
                AfirmacionesNoSustentadas = veredicto.AfirmacionesNoSustentadas,
            };
            ej.Resolucion = resolucion;

            if (veredicto.Bloquea)
            {
                t = Stopwatch.GetTimestamp();
                borrador = await generador.RegenerarAsync(borrador, veredicto.Fallas.Select(Describir).ToList(), s.ModeloId, ct);
                lGen += Ms(t);
                (tokensIn, tokensOut) = (tokensIn + borrador.TokensIn, tokensOut + borrador.TokensOut);
                ej.Regeneraciones = 1;
                Transicion(EstadoExpediente.Redactado);
                veredicto = Verificar(borrador.Borrador);
            }

            resolucion.AprobadaGuardrail = veredicto.Aprobado;
            if (veredicto.Bloquea)
            {
                Derivar("GUARDRAIL_SALIDA");
            }
            else
            {
                resolucion.TextoFinal = borrador.Borrador;
                Transicion(EstadoExpediente.Emitido);
            }
        }
    }

    /// <summary>Texto de cada falla para el mensaje correctivo (regeneracion.v1).</summary>
    public static string Describir(FallaGuardrail falla) => falla.Tipo switch
    {
        TipoFalla.CitaNoAdmitida => $"Citaste [F:{falla.Detalle}], que no está entre los fragmentos recibidos.",
        TipoFalla.HechoNoCoincide => $"Mencionaste un dato que no coincide con los hechos verificados ({falla.Detalle}).",
        TipoFalla.SinMarcadorDecision => $"Falta la línea exacta «DECISIÓN: {falla.Detalle}».",
        TipoFalla.SentidoContradictorio => $"La decisión indicada ({falla.Detalle}) contradice la decisión del sistema de reglas.",
        TipoFalla.FaltaNumeroReclamo => $"Falta el número de reclamo {falla.Detalle}.",
        TipoFalla.FaltaInstancia => "Faltan las instancias a las que puede acudir el usuario (Defensoría del Cliente Financiero, SBS o Indecopi).",
        TipoFalla.FaltaPlazo => "Falta indicar el plazo aplicable tal como figura en los hechos.",
        _ => falla.Detalle,
    };

    private static double Ms(long desde) => Stopwatch.GetElapsedTime(desde).TotalMilliseconds;

    private static int Redondear(double ms) => (int)Math.Round(ms, MidpointRounding.AwayFromZero);
}
