using System.Diagnostics;
using System.Text.Json;
using Reclamos.Domain.Enums;

namespace Reclamos.Application.Rag;

/// <summary>Resultado de la recuperación normativa; se persiste en <c>app.Recuperacion</c>.</summary>
public sealed record ResultadoRecuperacion(
    CalificacionRecuperacion Calificacion,
    IReadOnlyList<FragmentoRecuperado> Admitidos,
    IReadOnlyList<FragmentoEvaluado> Evaluados,
    string VersionConsulta,
    TimeSpan Duracion)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Incorrecta ⇒ el expediente se deriva (SPEC §2.4).</summary>
    public bool Derivar => Calificacion == CalificacionRecuperacion.Incorrecta;

    public string? MotivoDerivacion => Derivar ? "CRAG_INCORRECTA" : null;

    /// <summary><c>[{id, score, admitido, consulta}]</c> de cada consulta realizada.</summary>
    public string FragmentosJson => JsonSerializer.Serialize(Evaluados, Json);
}

/// <summary>
/// Plantillas de consulta (versión <c>consulta.v1</c>). La consulta depende solo de intención,
/// ruta y regla decididas por el Agente Legal; un cambio aquí es una nueva versión.
/// </summary>
public static class PlantillasConsulta
{
    public const string Version = "consulta.v1";

    private static readonly Dictionary<Intencion, string> Tipologia = new()
    {
        [Intencion.C1] = "cobro duplicado de una misma operación",
        [Intencion.C2] = "operación no reconocida por el titular",
        [Intencion.C3] = "operación fallida o no completada en la pasarela de pago con cargo registrado",
    };

    private static readonly Dictionary<string, string> Reglas = new()
    {
        ["R5"] = "existe un segundo cargo con igual monto y comercio dentro de las 24 horas",
        ["R6"] = "no existe un segundo cargo duplicado",
        ["R7"] = "la transacción figura como fallida o no completada y el cargo fue registrado",
        ["R9"] = "la operación se autenticó con autenticación reforzada desde el dispositivo registrado del titular",
    };

    private static readonly Dictionary<Intencion, string> Terminos = new()
    {
        [Intencion.C1] = "cobro duplicado, cargo indebido, extorno, devolución del importe",
        [Intencion.C2] = "operación no reconocida, autenticación, seguridad de las operaciones, responsabilidad",
        [Intencion.C3] = "pasarela de pago, operación no concluida, cargo indebido, restitución",
    };

    private const string Normas =
        "Res. SBS N.° 04036-2022 Reglamento de Gestión de Reclamos y Requerimientos; " +
        "Ley N.° 29571 Código de Protección y Defensa del Consumidor";

    public static string Consulta(ConsultaNormativa c)
    {
        var sentido = c.Ruta == Ruta.Procedente ? "procedente" : "improcedente";
        var regla = Reglas.TryGetValue(c.Regla, out var r) ? r : c.Regla;
        return $"Respuesta a un reclamo de un usuario del sistema financiero por {Tipologia[c.Intencion]}. " +
               $"El reclamo es {sentido} porque {regla}. " +
               "Plazo de atención, contenido de la respuesta e instancias a las que puede acudir el usuario.";
    }

    public static string Reformular(ConsultaNormativa c) =>
        $"{Consulta(c)} {Normas}. Términos: {Terminos[c.Intencion]}.";
}

/// <summary>
/// Control CRAG (SPEC §2.4): admite fragmentos con score ≥ θ; si no hay, reformula una vez;
/// si sigue sin haber, la recuperación es Incorrecta y el expediente se deriva.
/// </summary>
public sealed class RecuperadorCrag(RagOptions opciones, IBuscadorVectorial buscador) : INormativeRetriever
{
    public async Task<ResultadoRecuperacion> RecuperarAsync(ConsultaNormativa consulta, CancellationToken ct)
    {
        if (consulta.Ruta == Ruta.Derivar)
            throw new ArgumentException("Un expediente derivado no llega a recuperación.", nameof(consulta));

        var inicio = Stopwatch.GetTimestamp();
        if (!opciones.Enabled)
            return Resultado(CalificacionRecuperacion.Omitida, [], [], inicio);

        var evaluados = new List<FragmentoEvaluado>();

        var admitidos = await BuscarAsync(PlantillasConsulta.Consulta(consulta), 1, evaluados, ct);
        var calificacion = CalificacionRecuperacion.Correcta;
        if (admitidos.Count == 0)
        {
            admitidos = await BuscarAsync(PlantillasConsulta.Reformular(consulta), 2, evaluados, ct);
            calificacion = admitidos.Count > 0 ? CalificacionRecuperacion.Ambigua : CalificacionRecuperacion.Incorrecta;
        }

        return Resultado(calificacion, admitidos, evaluados, inicio);
    }

    private static ResultadoRecuperacion Resultado(
        CalificacionRecuperacion calificacion, List<FragmentoRecuperado> admitidos, List<FragmentoEvaluado> evaluados, long inicio) =>
        new(Calificacion: calificacion,
            Admitidos: admitidos,
            Evaluados: evaluados,
            VersionConsulta: PlantillasConsulta.Version,
            Duracion: Stopwatch.GetElapsedTime(inicio));

    private async Task<List<FragmentoRecuperado>> BuscarAsync(
        string texto, int numero, List<FragmentoEvaluado> evaluados, CancellationToken ct)
    {
        var encontrados = (await buscador.BuscarAsync(texto, opciones.TopK, ct))
            .OrderByDescending(f => f.Score)
            .ToList();
        evaluados.AddRange(encontrados.Select(f => new FragmentoEvaluado(f.Id, f.Score, f.Score >= opciones.Theta, numero)));
        return encontrados.Where(f => f.Score >= opciones.Theta).ToList();
    }
}
