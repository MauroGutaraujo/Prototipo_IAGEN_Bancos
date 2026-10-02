using System.Globalization;
using System.Text.Json;
using Json.Schema;
using Reclamos.Application.Llm;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;

namespace Reclamos.Application.Agentes;

/// <summary>Resultado de la clasificación con lo que se registra en Ejecucion.</summary>
public sealed record ResultadoClasificacion(
    SalidaClasificador Salida, int Intentos, string? ModeloVersion, int TokensIn, int TokensOut);

/// <summary>Clasificador de intención (LLM, salida JSON validada; SPEC §2.2). No decide la procedencia.</summary>
public interface IIntentClassifier
{
    Task<ResultadoClasificacion> ClasificarAsync(string narracion, string modeloId, CancellationToken ct);
}

/// <summary>
/// Llama al LLM con <c>clasificador.v1</c>; valida contra <c>clasificador.schema.json</c>. Si la salida no parsea o
/// no cumple el esquema, reintenta una vez; si vuelve a fallar, la salida queda inválida (el Agente Legal aplica R4).
/// </summary>
public sealed class ClasificadorIntencion : IIntentClassifier
{
    private const int MaxIntentos = 2;

    private readonly IClienteLlm _llm;
    private readonly PlantillaPrompt _prompt;
    private readonly JsonSchema _esquema;
    private readonly ParametrosLlm _parametros;

    public ClasificadorIntencion(IClienteLlm llm, PlantillaPrompt prompt, string esquemaJson, ParametrosLlm parametros)
    {
        _llm = llm;
        _prompt = prompt;
        _esquema = JsonSchema.FromText(esquemaJson);
        _parametros = parametros;
    }

    public async Task<ResultadoClasificacion> ClasificarAsync(string narracion, string modeloId, CancellationToken ct)
    {
        List<MensajeChat> mensajes =
        [
            new(RolMensaje.Sistema, _prompt.Sistema ?? ""),
            new(RolMensaje.Usuario, Renderizador.Renderizar(_prompt.Usuario, new Dictionary<string, string> { ["narracion"] = narracion })),
        ];

        int tokensIn = 0, tokensOut = 0;
        string? version = null;
        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            var r = await _llm.CompletarAsync(modeloId, mensajes, _parametros.MaxTokensClasificacion, salidaJson: true, ct);
            tokensIn += r.TokensIn ?? 0;
            tokensOut += r.TokensOut ?? 0;
            version ??= r.ModeloVersion;
            if (Interpretar(r.Texto) is { } salida)
                return new ResultadoClasificacion(salida, intento, version, tokensIn, tokensOut);
        }

        return new ResultadoClasificacion(new SalidaClasificador(false, null, null, null, null, null),
            MaxIntentos, version, tokensIn, tokensOut);
    }

    /// <summary>Salida válida según el esquema, o null.</summary>
    private SalidaClasificador? Interpretar(string texto)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(texto);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var raiz = doc.RootElement;
            if (!_esquema.Evaluate(raiz, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid)
                return null;

            DateOnly? fecha = null;
            if (raiz.GetProperty("fecha").ValueKind == JsonValueKind.String)
            {
                if (!DateOnly.TryParseExact(raiz.GetProperty("fecha").GetString(), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                {
                    return null; // cumple el patrón pero no es una fecha de calendario
                }
                fecha = f;
            }

            return new SalidaClasificador(
                EsValida: true,
                Intencion: raiz.GetProperty("intencion").GetString() switch
                {
                    "C1" => Intencion.C1,
                    "C2" => Intencion.C2,
                    "C3" => Intencion.C3,
                    _ => Intencion.FueraDeCatalogo,
                },
                Monto: raiz.GetProperty("monto").ValueKind == JsonValueKind.Number ? raiz.GetProperty("monto").GetDecimal() : null,
                Moneda: raiz.GetProperty("moneda").GetString() switch { "PEN" => Moneda.PEN, "USD" => Moneda.USD, _ => null },
                Fecha: fecha,
                Codigo: raiz.GetProperty("codigo").GetString());
        }
    }
}
