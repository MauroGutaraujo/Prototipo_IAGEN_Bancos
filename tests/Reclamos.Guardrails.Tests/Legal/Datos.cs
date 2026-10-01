using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;

namespace Reclamos.Guardrails.Tests.Legal;

/// <summary>Escenario base consistente (C1 sin duplicado ⇒ R6); cada prueba cambia solo lo que necesita.</summary>
internal static class Datos
{
    public const string Dispositivo = "DEV-000001";
    public static readonly DateTime Fecha = new(2026, 3, 10, 12, 0, 0);

    public static GuardrailOptions Opciones(int ventanaHoras = 24) => new()
    {
        UmbralOcr = 0.90m,
        UmbralRiesgoPen = 1000.00m,
        VentanaDuplicadoHoras = ventanaHoras,
        PlazoRespuesta = "plazo de prueba P-01",
    };

    public static TransaccionCore Tx(
        decimal monto = 150.50m,
        Moneda moneda = Moneda.PEN,
        EstadoTransaccion estado = EstadoTransaccion.Completada,
        bool auth = true,
        string? dispositivo = Dispositivo,
        bool riesgo = false) =>
        new("AN12345678", 1, monto, moneda, Fecha, "Comercio X", estado, auth, dispositivo, riesgo);

    public static ResultadoOcr Ocr(TransaccionCore tx, decimal conf = 0.95m) =>
        new(tx.Monto, tx.Moneda, tx.FechaHora, tx.Codigo, conf, conf, conf);

    public static SalidaClasificador Cls(TransaccionCore tx, Intencion intencion = Intencion.C1) =>
        new(true, intencion, tx.Monto, tx.Moneda, DateOnly.FromDateTime(tx.FechaHora), tx.Codigo);

    public static HechosCore Core(TransaccionCore tx, decimal? tipoCambio = null, params TransaccionCore[] otros) =>
        new(tx, [tx, .. otros], tipoCambio, Dispositivo);

    public static EntradaLegal Entrada(TransaccionCore tx, Intencion intencion = Intencion.C1) =>
        new(Ocr(tx), Cls(tx, intencion), Core(tx));

    public static DictamenLegal Decidir(EntradaLegal entrada, int ventanaHoras = 24) =>
        new AgenteLegal(Opciones(ventanaHoras)).Decidir(entrada);
}
