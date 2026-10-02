using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reclamos.Application.Llm;

/// <summary>Un modelo del registro (config/models.json). La clave se lee de la variable <see cref="ApiKeyEnv"/>.</summary>
public sealed record ModeloLlm(
    string Id, string Proveedor, string Segmento, string Endpoint, string ApiKeyEnv, string ModelName);

/// <summary>Parámetros comunes a todos los modelos (iguales en el benchmark).</summary>
public sealed record ParametrosLlm(double Temperature, int MaxTokensClasificacion, int MaxTokensGeneracion);

/// <summary>Registro de modelos por configuración: cambiar de modelo no requiere cambiar código.</summary>
public sealed class RegistroModelos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [JsonConstructor]
    public RegistroModelos(IReadOnlyList<ModeloLlm> modelos, ParametrosLlm parametros)
    {
        Modelos = modelos;
        Parametros = parametros;
    }

    public IReadOnlyList<ModeloLlm> Modelos { get; }

    public ParametrosLlm Parametros { get; }

    public static RegistroModelos Desde(string json) =>
        JsonSerializer.Deserialize<RegistroModelos>(json, Json)
        ?? throw new InvalidOperationException("Registro de modelos vacío");

    public ModeloLlm? Buscar(string id) => Modelos.FirstOrDefault(m => m.Id == id);
}

public enum RolMensaje
{
    Sistema,
    Usuario,
    Asistente,
}

public sealed record MensajeChat(RolMensaje Rol, string Texto);

/// <summary>Respuesta de una llamada al LLM con lo que se registra en Ejecucion.</summary>
public sealed record RespuestaLlm(string Texto, string? ModeloVersion, int? TokensIn, int? TokensOut);

/// <summary>Puerto hacia el LLM (la implementación con Semantic Kernel vive en Infrastructure).</summary>
public interface IClienteLlm
{
    Task<RespuestaLlm> CompletarAsync(
        string modeloId, IReadOnlyList<MensajeChat> mensajes, int maxTokens, bool salidaJson, CancellationToken ct);
}
