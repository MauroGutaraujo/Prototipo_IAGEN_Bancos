using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using Reclamos.Application.Llm;

namespace Reclamos.Infrastructure.Llm;

/// <summary>Configuración del cliente LLM (sección <c>Llm</c>).</summary>
public sealed class LlmOptions
{
    public const string Seccion = "Llm";

    /// <summary>Registro de modelos (sin claves): id, proveedor, endpoint, apiKeyEnv, modelName.</summary>
    public string RegistroPath { get; set; } = "config/models.json";

    /// <summary>Reintentos automáticos de transporte (SPEC §2.2: 0, para no inflar la latencia medida).</summary>
    public int ReintentosTransporte { get; set; }

    public int TimeoutSegundos { get; set; } = 60;
}

/// <summary>
/// <see cref="IClienteLlm"/> con Semantic Kernel y el conector OpenAI apuntando al endpoint compatible de cada
/// proveedor. Registra el modelo que responde la API y los tokens de la respuesta.
/// </summary>
public sealed class ClienteLlmSemanticKernel : IClienteLlm
{
    private readonly RegistroModelos _registro;
    private readonly Func<ModeloLlm, IChatCompletionService> _fabrica;
    private readonly ConcurrentDictionary<string, IChatCompletionService> _servicios = new();

    public ClienteLlmSemanticKernel(RegistroModelos registro, LlmOptions opciones, Func<ModeloLlm, IChatCompletionService>? fabrica = null)
    {
        _registro = registro;
        _fabrica = fabrica ?? (m => Crear(m, opciones));
    }

    public async Task<RespuestaLlm> CompletarAsync(
        string modeloId, IReadOnlyList<MensajeChat> mensajes, int maxTokens, bool salidaJson, CancellationToken ct)
    {
        var modelo = _registro.Buscar(modeloId) ?? throw new ArgumentException($"Modelo no registrado: {modeloId}", nameof(modeloId));
        var servicio = _servicios.GetOrAdd(modelo.Id, _ => _fabrica(modelo));

        var historia = new ChatHistory();
        foreach (var m in mensajes)
        {
            switch (m.Rol)
            {
                case RolMensaje.Sistema:
                    historia.AddSystemMessage(m.Texto);
                    break;
                case RolMensaje.Asistente:
                    historia.AddAssistantMessage(m.Texto);
                    break;
                default:
                    historia.AddUserMessage(m.Texto);
                    break;
            }
        }

        var ajustes = new OpenAIPromptExecutionSettings
        {
            Temperature = _registro.Parametros.Temperature,
            MaxTokens = maxTokens,
        };
        if (salidaJson)
            ajustes.ResponseFormat = "json_object";

        var respuesta = await servicio.GetChatMessageContentAsync(historia, ajustes, kernel: null, ct);
        var cruda = respuesta.InnerContent as OpenAI.Chat.ChatCompletion;
        return new RespuestaLlm(
            respuesta.Content ?? "",
            cruda?.Model ?? respuesta.ModelId,
            cruda?.Usage?.InputTokenCount,
            cruda?.Usage?.OutputTokenCount);
    }

    private static IChatCompletionService Crear(ModeloLlm modelo, LlmOptions opciones)
    {
        var clave = Environment.GetEnvironmentVariable(modelo.ApiKeyEnv);
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException($"Falta la variable de entorno {modelo.ApiKeyEnv} para el modelo {modelo.Id}");

        var cliente = new OpenAIClient(new ApiKeyCredential(clave), new OpenAIClientOptions
        {
            Endpoint = new Uri(modelo.Endpoint),
            RetryPolicy = new ClientRetryPolicy(opciones.ReintentosTransporte),
            NetworkTimeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos),
        });
        return new OpenAIChatCompletionService(modelo.ModelName, cliente);
    }
}
