using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using Reclamos.Application.Llm;
using Reclamos.Infrastructure.Llm;
using SkChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace Reclamos.Infrastructure.Tests.Llm;

public class ClienteLlmSemanticKernelTests
{
    /// <summary>IChatCompletionService falso: registra la historia y los ajustes y responde una ChatCompletion.</summary>
    private sealed class ChatFalso(string texto) : IChatCompletionService
    {
        public ChatHistory? Historia { get; private set; }
        public OpenAIPromptExecutionSettings? Ajustes { get; private set; }

        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<SkChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            Historia = chatHistory;
            Ajustes = (OpenAIPromptExecutionSettings?)executionSettings;
            // La fábrica del SDK es experimental (OPENAI001); solo se usa aquí para construir una respuesta de prueba.
#pragma warning disable OPENAI001
            var cruda = OpenAIChatModelFactory.ChatCompletion(
                model: "gemini-version-del-proveedor",
                usage: OpenAIChatModelFactory.ChatTokenUsage(outputTokenCount: 7, inputTokenCount: 42, totalTokenCount: 49));
#pragma warning restore OPENAI001
            IReadOnlyList<SkChatMessageContent> r = [new SkChatMessageContent(AuthorRole.Assistant, texto, "modelo-configurado", cruda)];
            return Task.FromResult(r);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static readonly RegistroModelos Registro = new(
        [new ModeloLlm("dev", "Google", "desarrollo", "https://ejemplo/v1/", "CLAVE_INEXISTENTE", "modelo-x")],
        new ParametrosLlm(0, 200, 600));

    [Fact]
    public async Task Envia_roles_temperatura_tokens_y_json_y_registra_modelo_y_tokens()
    {
        var chat = new ChatFalso("{}");
        var cliente = new ClienteLlmSemanticKernel(Registro, new LlmOptions(), _ => chat);

        var r = await cliente.CompletarAsync("dev",
            [new(RolMensaje.Sistema, "s"), new(RolMensaje.Usuario, "u"), new(RolMensaje.Asistente, "a"), new(RolMensaje.Usuario, "u2")],
            200, salidaJson: true, TestContext.Current.CancellationToken);

        Assert.Equal(("{}", "gemini-version-del-proveedor", 42, 7), (r.Texto, r.ModeloVersion, r.TokensIn, r.TokensOut));
        Assert.Equal([AuthorRole.System, AuthorRole.User, AuthorRole.Assistant, AuthorRole.User], chat.Historia!.Select(m => m.Role));
        Assert.Equal(0, chat.Ajustes!.Temperature);
        Assert.Equal(200, chat.Ajustes.MaxTokens);
        Assert.Equal("json_object", chat.Ajustes.ResponseFormat);
    }

    [Fact]
    public async Task Sin_json_no_fija_formato_de_respuesta()
    {
        var chat = new ChatFalso("texto");
        await new ClienteLlmSemanticKernel(Registro, new LlmOptions(), _ => chat)
            .CompletarAsync("dev", [new(RolMensaje.Usuario, "u")], 600, salidaJson: false, TestContext.Current.CancellationToken);
        Assert.Null(chat.Ajustes!.ResponseFormat);
    }

    [Fact]
    public async Task Modelo_no_registrado_o_sin_clave_falla()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new ClienteLlmSemanticKernel(Registro, new LlmOptions())
            .CompletarAsync("otro", [], 10, false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClienteLlmSemanticKernel(Registro, new LlmOptions())
            .CompletarAsync("dev", [new(RolMensaje.Usuario, "u")], 10, false, TestContext.Current.CancellationToken));
    }
}
