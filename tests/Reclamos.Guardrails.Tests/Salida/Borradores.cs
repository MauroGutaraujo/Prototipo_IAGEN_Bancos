using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Salida;

namespace Reclamos.Guardrails.Tests.Salida;

/// <summary>Borrador base que cumple las 4 verificaciones; cada prueba lo altera.</summary>
internal static class Borradores
{
    /// <summary>Plazo ficticio: el valor real lo fija el estudiante desde la normativa.</summary>
    public const string Plazo = "plazo de prueba P-01";

    public static readonly HechosVerificados Hechos =
        new(123, 150.50m, Moneda.PEN, new DateTime(2026, 3, 10, 12, 0, 0), "AN12345678");

    public static readonly string[] Admitidos = ["sbs-art-10", "ley29571-art-88"];

    public const string Valido = """
        Estimado cliente:

        En atención a su reclamo N.° 123, sobre la operación AN12345678 por S/ 150.50 realizada el 10/03/2026 a las 12:00, le informamos que, conforme a [F:sbs-art-10], corresponde la devolución del cargo.

        DECISIÓN: PROCEDENTE

        La devolución se realizará dentro del plazo de prueba P-01.

        Si no se encuentra conforme, puede acudir a la Defensoría del Cliente Financiero, a la SBS o a Indecopi.
        """;

    public static GuardrailOptions Opciones(string plazo = Plazo) => new() { PlazoRespuesta = plazo };

    public static VeredictoGuardrail Verificar(
        string borrador,
        Ruta ruta = Ruta.Procedente,
        HechosVerificados? hechos = null,
        IReadOnlyCollection<string>? admitidos = null,
        bool ablacion = false,
        string plazo = Plazo) =>
        new GuardrailSalida(Opciones(plazo)).Verificar(
            new EntradaGuardrailSalida(borrador, ruta, hechos ?? Hechos, admitidos ?? Admitidos, ablacion));

    public static IEnumerable<TipoFalla> Tipos(VeredictoGuardrail v) => v.Fallas.Select(f => f.Tipo);
}
