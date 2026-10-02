using System.Globalization;
using Reclamos.Application.Llm;
using Reclamos.Application.Rag;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails.Legal;

namespace Reclamos.Application.Agentes;

/// <summary>Lo que el generador recibe: decisión ya tomada, hechos verificados y fragmentos admitidos.</summary>
public sealed record SolicitudResolucion(
    int NumeroReclamo,
    Ruta Ruta,
    string Regla,
    string Motivo,
    Intencion Intencion,
    TransaccionCore Transaccion,
    IReadOnlyList<FragmentoRecuperado> Fragmentos);

/// <summary>Borrador y conversación (para regenerar), con lo que se registra en Ejecucion.</summary>
public sealed record ResultadoGeneracion(
    string Borrador, IReadOnlyList<MensajeChat> Conversacion, string? ModeloVersion, int TokensIn, int TokensOut);

/// <summary>Agente generativo (SPEC §2.5): redacta; no decide.</summary>
public interface IResolutionGenerator
{
    Task<ResultadoGeneracion> GenerarAsync(SolicitudResolucion solicitud, string modeloId, CancellationToken ct);

    /// <summary>Una regeneración con el mensaje correctivo <c>regeneracion.v1</c> y las fallas del guardrail.</summary>
    Task<ResultadoGeneracion> RegenerarAsync(
        ResultadoGeneracion previo, IReadOnlyList<string> fallas, string modeloId, CancellationToken ct);
}

/// <summary>Formatos de los hechos tal como los verifica el guardrail de salida (SPEC §2.5–§2.6).</summary>
public static class FormatoHechos
{
    public static string Monto(decimal monto, Moneda moneda) =>
        $"{(moneda == Moneda.PEN ? "S/" : "US$")} {monto.ToString("N2", CultureInfo.InvariantCulture)}";

    public static string FechaHora(DateTime fechaHora) => fechaHora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Tipologia(Intencion intencion) => intencion switch
    {
        Intencion.C1 => "Cobro duplicado",
        Intencion.C2 => "Operación no reconocida",
        Intencion.C3 => "Caída de pasarela u operación no completada",
        _ => "Fuera de catálogo",
    };

    public static string Estado(EstadoTransaccion estado) => estado switch
    {
        EstadoTransaccion.Completada => "Completada",
        EstadoTransaccion.Fallida => "Fallida",
        _ => "No completada",
    };
}

public sealed class GeneradorResolucion(
    IClienteLlm llm, PlantillaPrompt generador, PlantillaPrompt regeneracion, ParametrosLlm parametros, string plazo)
    : IResolutionGenerator
{
    public async Task<ResultadoGeneracion> GenerarAsync(SolicitudResolucion s, string modeloId, CancellationToken ct)
    {
        var tx = s.Transaccion;
        var valores = new Dictionary<string, string>
        {
            ["decision"] = s.Ruta == Ruta.Procedente ? "PROCEDENTE" : "IMPROCEDENTE",
            ["idExpediente"] = s.NumeroReclamo.ToString(CultureInfo.InvariantCulture),
            ["regla"] = s.Regla,
            ["motivo"] = s.Motivo,
            ["tipologia"] = FormatoHechos.Tipologia(s.Intencion),
            ["monto"] = FormatoHechos.Monto(tx.Monto, tx.Moneda),
            ["fechaHora"] = FormatoHechos.FechaHora(tx.FechaHora),
            ["codigo"] = tx.Codigo,
            ["comercio"] = tx.Comercio,
            ["estado"] = FormatoHechos.Estado(tx.Estado),
            ["plazo"] = plazo,
        };
        var listas = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
        {
            ["fragmentos"] = s.Fragmentos
                .Select(f => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["id"] = f.Id, ["texto"] = f.Texto })
                .ToList(),
        };

        List<MensajeChat> conversacion =
        [
            new(RolMensaje.Sistema, Renderizador.Renderizar(generador.Sistema ?? "", valores, listas)),
            new(RolMensaje.Usuario, Renderizador.Renderizar(generador.Usuario, valores, listas)),
        ];
        return await LlamarAsync(conversacion, modeloId, ct);
    }

    public async Task<ResultadoGeneracion> RegenerarAsync(
        ResultadoGeneracion previo, IReadOnlyList<string> fallas, string modeloId, CancellationToken ct)
    {
        var correccion = Renderizador.Renderizar(regeneracion.Usuario, new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>
            {
                ["fallas"] = fallas
                    .Select(f => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["descripcion"] = f })
                    .ToList(),
            });
        List<MensajeChat> conversacion =
        [
            .. previo.Conversacion,
            new(RolMensaje.Asistente, previo.Borrador),
            new(RolMensaje.Usuario, correccion),
        ];
        return await LlamarAsync(conversacion, modeloId, ct);
    }

    private async Task<ResultadoGeneracion> LlamarAsync(List<MensajeChat> conversacion, string modeloId, CancellationToken ct)
    {
        var r = await llm.CompletarAsync(modeloId, conversacion, parametros.MaxTokensGeneracion, salidaJson: false, ct);
        return new ResultadoGeneracion(r.Texto, conversacion, r.ModeloVersion, r.TokensIn ?? 0, r.TokensOut ?? 0);
    }
}
