using Reclamos.Application.Llm;

namespace Reclamos.Application.Tests.Llm;

/// <summary>Cliente LLM falso: devuelve las respuestas en orden y registra las llamadas.</summary>
internal sealed class LlmFalso(params string[] respuestas) : IClienteLlm
{
    public List<(string Modelo, IReadOnlyList<MensajeChat> Mensajes, int MaxTokens, bool Json)> Llamadas { get; } = [];

    public Task<RespuestaLlm> CompletarAsync(
        string modeloId, IReadOnlyList<MensajeChat> mensajes, int maxTokens, bool salidaJson, CancellationToken ct)
    {
        var texto = respuestas[Llamadas.Count];
        Llamadas.Add((modeloId, mensajes, maxTokens, salidaJson));
        return Task.FromResult(new RespuestaLlm(texto, "modelo-version-001", 10, 5));
    }
}
