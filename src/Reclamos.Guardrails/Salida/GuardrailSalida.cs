using System.Globalization;
using System.Text.RegularExpressions;
using Reclamos.Domain.Enums;

namespace Reclamos.Guardrails.Salida;

/// <summary>
/// Guardrail de salida (SPEC §2.6). Determinista, sin LLM ni IO.
/// Afirmación verificable = cada cita [F:id] y cada mención de monto, fecha o código;
/// no sustentada = la que no coincide con los hechos o cita un fragmento no admitido.
/// </summary>
public sealed class GuardrailSalida
{
    private const RegexOptions Opc = RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Regex Cita = new(@"\[F:([^\]\s]+)\]", Opc);
    private static readonly Regex Monto = new(@"(S/|US\$)\s?(\d{1,3}(?:,\d{3})+(?:\.\d{2})?|\d+(?:\.\d{2})?)", Opc);
    private static readonly Regex FechaNumerica = new(@"\b(\d{1,2})[/-](\d{1,2})[/-](\d{4})\b", Opc);
    private static readonly Regex FechaTexto = new(
        @"\b(\d{1,2})\s+de\s+(enero|febrero|marzo|abril|mayo|junio|julio|agosto|setiembre|septiembre|octubre|noviembre|diciembre)\s+del?\s+(\d{4})\b",
        Opc | RegexOptions.IgnoreCase);
    private static readonly Regex Codigo = new(@"\b(?=[A-Z0-9]*[A-Z])(?=[A-Z0-9]*\d)[A-Z0-9]{8,12}\b", Opc);
    private static readonly Regex Hora = new(@"\b\d{1,2}:\d{2}\b", Opc);
    private static readonly Regex Marcador = new(
        @"^\s*DECISI[ÓO]N\s*:\s*(PROCEDENTE|IMPROCEDENTE)\s*$", Opc | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex Instancia = new(
        @"defensoria del cliente financiero|\bsbs\b|\bindecopi\b", Opc);

    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio",
        "agosto", "setiembre", "octubre", "noviembre", "diciembre",
    ];

    private readonly string _plazo;

    public GuardrailSalida(GuardrailOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        ArgumentException.ThrowIfNullOrWhiteSpace(opciones.PlazoRespuesta, "Guardrail:PlazoRespuesta");
        _plazo = Normalizacion.Texto(opciones.PlazoRespuesta);
    }

    public VeredictoGuardrail Verificar(EntradaGuardrailSalida entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (entrada.Ruta == Ruta.Derivar)
            throw new ArgumentException("Un expediente derivado no llega a redacción.", nameof(entrada));

        var fallas = new List<FallaGuardrail>();
        var verificables = 0;
        var resto = entrada.Borrador;
        var h = entrada.Hechos;

        // 1. Citas
        foreach (Match m in Cita.Matches(resto))
        {
            verificables++;
            if (!entrada.FragmentosAdmitidos.Contains(m.Groups[1].Value))
                fallas.Add(new(TipoFalla.CitaNoAdmitida, m.Groups[1].Value));
        }
        resto = Cita.Replace(resto, " ");

        // 2. Hechos: monto, fecha y código
        foreach (Match m in Monto.Matches(resto))
        {
            verificables++;
            var moneda = m.Groups[1].Value == "S/" ? Moneda.PEN : Moneda.USD;
            var valor = decimal.Parse(m.Groups[2].Value.Replace(",", ""), CultureInfo.InvariantCulture);
            if (moneda != h.Moneda || valor != h.Monto)
                fallas.Add(new(TipoFalla.HechoNoCoincide, $"monto:{m.Value}"));
        }
        resto = Monto.Replace(resto, " ");

        var fechaHechos = DateOnly.FromDateTime(h.FechaHora);
        foreach (var (texto, fecha) in Fechas(resto))
        {
            verificables++;
            if (fecha != fechaHechos)
                fallas.Add(new(TipoFalla.HechoNoCoincide, $"fecha:{texto}"));
        }
        resto = FechaTexto.Replace(FechaNumerica.Replace(resto, " "), " ");

        foreach (Match m in Codigo.Matches(resto))
        {
            verificables++;
            if (m.Value != Normalizacion.Codigo(h.Codigo))
                fallas.Add(new(TipoFalla.HechoNoCoincide, $"codigo:{m.Value}"));
        }
        resto = Hora.Replace(Codigo.Replace(resto, " "), " ");

        var noSustentadas = fallas.Count;

        // 3. Sentido
        var esperado = entrada.Ruta == Ruta.Procedente ? "PROCEDENTE" : "IMPROCEDENTE";
        var marcadores = Marcador.Matches(entrada.Borrador).Select(m => m.Groups[1].Value.ToUpperInvariant()).ToList();
        if (marcadores.Count == 0)
            fallas.Add(new(TipoFalla.SinMarcadorDecision, esperado));
        fallas.AddRange(marcadores.Where(m => m != esperado).Select(m => new FallaGuardrail(TipoFalla.SentidoContradictorio, m)));

        // 4. Contenidos mínimos (el número se busca fuera de montos, fechas, códigos, horas y citas)
        if (!Regex.IsMatch(resto, $@"(?<!\d)0*{h.NumeroReclamo}(?!\d)", RegexOptions.CultureInvariant))
            fallas.Add(new(TipoFalla.FaltaNumeroReclamo, h.NumeroReclamo.ToString(CultureInfo.InvariantCulture)));
        // Sin citas: un id como [F:sbs-art-10] no cuenta como mención de la SBS.
        var normalizado = Normalizacion.Texto(Cita.Replace(entrada.Borrador, " "));
        if (!Instancia.IsMatch(normalizado))
            fallas.Add(new(TipoFalla.FaltaInstancia, "Defensoría del Cliente Financiero | SBS | Indecopi"));
        if (!normalizado.Contains(_plazo, StringComparison.Ordinal))
            fallas.Add(new(TipoFalla.FaltaPlazo, _plazo));

        return new VeredictoGuardrail(fallas, verificables, noSustentadas, entrada.ModoAblacion);
    }

    /// <summary>Fechas mencionadas; las imposibles (p. ej. 31/02) se devuelven como null.</summary>
    private static List<(string Texto, DateOnly? Fecha)> Fechas(string texto)
    {
        var fechas = new List<(string, DateOnly?)>();
        foreach (Match m in FechaNumerica.Matches(texto))
            fechas.Add((m.Value, Crear(m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value)));

        foreach (Match m in FechaTexto.Matches(texto))
        {
            var nombre = m.Groups[2].Value.ToLowerInvariant();
            var mes = nombre == "septiembre" ? 9 : Array.IndexOf(Meses, nombre) + 1;
            fechas.Add((m.Value, Crear(m.Groups[3].Value, mes.ToString(CultureInfo.InvariantCulture), m.Groups[1].Value)));
        }
        return fechas;
    }

    private static DateOnly? Crear(string anio, string mes, string dia)
    {
        var a = int.Parse(anio, CultureInfo.InvariantCulture);
        var m = int.Parse(mes, CultureInfo.InvariantCulture);
        var d = int.Parse(dia, CultureInfo.InvariantCulture);
        return m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(a, m) ? new DateOnly(a, m, d) : null;
    }
}
