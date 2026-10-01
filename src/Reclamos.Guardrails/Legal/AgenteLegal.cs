using Reclamos.Domain.Enums;

namespace Reclamos.Guardrails.Legal;

/// <summary>
/// Agente Legal: reglas R1–R9 (SPEC §2.3). Determinista, sin LLM ni IO.
/// Evalúa en orden; la primera regla que dispara define la ruta.
/// </summary>
public sealed class AgenteLegal
{
    private readonly GuardrailOptions _opciones;

    public AgenteLegal(GuardrailOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones;
    }

    public DictamenLegal Decidir(EntradaLegal entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var tx = entrada.Core.Transaccion;

        return R1(entrada.Ocr)
            ?? R2(entrada.Clasificador, entrada.Ocr, tx)
            ?? R3(tx, entrada.Core.TipoCambioUsdPen)
            ?? R4(entrada.Clasificador)
            ?? entrada.Clasificador.Intencion switch
            {
                Intencion.C1 => R5R6(tx, entrada.Core.MovimientosCliente),
                Intencion.C3 => R7(tx),
                _ => R8R9(tx, entrada.Core.DispositivoRegistrado),
            };
    }

    private DictamenLegal? R1(ResultadoOcr ocr)
    {
        var invalidos = new List<string>(3);
        if (ocr.Monto is null || ocr.Moneda is null || ocr.ConfMonto < _opciones.UmbralOcr)
            invalidos.Add("monto");
        if (ocr.FechaHora is null || ocr.ConfFecha < _opciones.UmbralOcr)
            invalidos.Add("fecha");
        if (string.IsNullOrWhiteSpace(ocr.Codigo) || ocr.ConfCodigo < _opciones.UmbralOcr)
            invalidos.Add("codigo");

        return Derivar("R1", MotivoLegal.OcrIlegible, invalidos);
    }

    /// <remarks>R1 ya garantizó que los campos OCR tienen valor.</remarks>
    private static DictamenLegal? R2(SalidaClasificador cls, ResultadoOcr ocr, TransaccionCore tx)
    {
        var discrepancias = new List<string>(6);
        if (cls.EsValida)
        {
            if (cls.Monto is { } monto && (monto != tx.Monto || (cls.Moneda is { } moneda && moneda != tx.Moneda)))
                discrepancias.Add("texto.monto");
            if (cls.Fecha is { } fecha && fecha != DateOnly.FromDateTime(tx.FechaHora))
                discrepancias.Add("texto.fecha");
            if (cls.Codigo is { } codigo && Normalizacion.Codigo(codigo) != Normalizacion.Codigo(tx.Codigo))
                discrepancias.Add("texto.codigo");
        }

        if (ocr.Monto != tx.Monto || ocr.Moneda != tx.Moneda)
            discrepancias.Add("ocr.monto");
        if (ocr.FechaHora!.Value.Date != tx.FechaHora.Date)
            discrepancias.Add("ocr.fecha");
        if (Normalizacion.Codigo(ocr.Codigo!) != Normalizacion.Codigo(tx.Codigo))
            discrepancias.Add("ocr.codigo");

        return Derivar("R2", MotivoLegal.Discrepancia, discrepancias);
    }

    private DictamenLegal? R3(TransaccionCore tx, decimal? tipoCambio)
    {
        decimal montoPen;
        if (tx.Moneda == Moneda.PEN)
        {
            montoPen = tx.Monto;
        }
        else if (tipoCambio is { } tc)
        {
            montoPen = tx.Monto * tc;
        }
        else
        {
            return new DictamenLegal(Ruta.Derivar, "R3", MotivoLegal.TipoCambioNoDisponible);
        }

        return montoPen > _opciones.UmbralRiesgoPen
            ? new DictamenLegal(Ruta.Derivar, "R3", MotivoLegal.MontoSuperaUmbral)
            : null;
    }

    private static DictamenLegal? R4(SalidaClasificador cls) => cls switch
    {
        { EsValida: false } or { Intencion: null } => new DictamenLegal(Ruta.Derivar, "R4", MotivoLegal.ClasificadorInvalido),
        { Intencion: Intencion.FueraDeCatalogo } => new DictamenLegal(Ruta.Derivar, "R4", MotivoLegal.FueraDeCatalogo),
        _ => null,
    };

    private DictamenLegal R5R6(TransaccionCore tx, IReadOnlyList<TransaccionCore> movimientos)
    {
        var ventana = TimeSpan.FromHours(_opciones.VentanaDuplicadoHoras);
        var duplicado = movimientos.Any(o =>
            o.Codigo != tx.Codigo
            && o.IdCliente == tx.IdCliente
            && o.Monto == tx.Monto
            && o.Moneda == tx.Moneda
            && o.Comercio == tx.Comercio
            && (o.FechaHora - tx.FechaHora).Duration() <= ventana);

        return duplicado
            ? new DictamenLegal(Ruta.Procedente, "R5", MotivoLegal.CargoDuplicado)
            : new DictamenLegal(Ruta.Improcedente, "R6", MotivoLegal.SinCargoDuplicado);
    }

    private static DictamenLegal R7(TransaccionCore tx) =>
        tx.Estado is EstadoTransaccion.Fallida or EstadoTransaccion.NoCompletada && tx.Monto > 0
            ? new DictamenLegal(Ruta.Procedente, "R7", MotivoLegal.OperacionNoCompletada)
            : new DictamenLegal(Ruta.Derivar, "R7", MotivoLegal.Conciliacion);

    private static DictamenLegal R8R9(TransaccionCore tx, string dispositivoRegistrado)
    {
        var senales = new List<string>(3);
        if (!tx.AutenticacionReforzada)
            senales.Add(MotivoLegal.SinAutenticacionReforzada);
        if (tx.IndicadorRiesgo)
            senales.Add(MotivoLegal.IndicadorRiesgo);
        if (tx.Dispositivo != dispositivoRegistrado)
            senales.Add(MotivoLegal.DispositivoNoRegistrado);

        return senales.Count > 0
            ? new DictamenLegal(Ruta.Derivar, "R8", string.Join(',', senales))
            : new DictamenLegal(Ruta.Improcedente, "R9", MotivoLegal.OperacionAutenticada);
    }

    private static DictamenLegal? Derivar(string regla, string motivo, List<string> detalles) =>
        detalles.Count == 0 ? null : new DictamenLegal(Ruta.Derivar, regla, $"{motivo}:{string.Join(',', detalles)}");
}
