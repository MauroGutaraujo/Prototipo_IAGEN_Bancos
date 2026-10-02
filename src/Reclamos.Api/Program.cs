using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reclamos.Api;
using Reclamos.Application.Agentes;
using Reclamos.Application.Llm;
using Reclamos.Application.Orquestacion;
using Reclamos.Application.Rag;
using Reclamos.Domain.Enums;
using Reclamos.Guardrails;
using Reclamos.Guardrails.Legal;
using Reclamos.Guardrails.Salida;
using Reclamos.Infrastructure;
using Reclamos.Infrastructure.Llm;
using Reclamos.Infrastructure.Ocr;
using Reclamos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Cadena de conexión: ConnectionStrings:Reclamos (user-secrets) o variable de entorno SQL_CONN.
var connectionString = builder.Configuration.GetConnectionString("Reclamos")
    ?? builder.Configuration["SQL_CONN"]
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión: define SQL_CONN o ConnectionStrings:Reclamos (user-secrets).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.Seccion));
builder.Services.AddOcr();
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection(RagOptions.Seccion));
builder.Services.PostConfigure<RagOptions>(o =>
{
    if (builder.Configuration["PINECONE_INDEX"] is { Length: > 0 } indice)
        o.Index = indice;
});
builder.Services.AddRag(builder.Configuration["PINECONE_API_KEY"]);

// Agentes simbólicos con los parámetros de la tesis (sección "Guardrail").
// GuardrailSalida exige Guardrail:PlazoRespuesta: falla al resolverse si no está configurado.
builder.Services.Configure<GuardrailOptions>(builder.Configuration.GetSection(GuardrailOptions.Seccion));
builder.Services.AddSingleton<ILegalAgent>(sp =>
    new LegalAgent(new AgenteLegal(sp.GetRequiredService<IOptions<GuardrailOptions>>().Value)));
builder.Services.AddSingleton<IOutputGuardrail>(sp =>
    new OutputGuardrail(new GuardrailSalida(sp.GetRequiredService<IOptions<GuardrailOptions>>().Value)));

// Clasificador, generador (LLM por config/models.json) y orquestador secuencial.
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.Seccion));
builder.Services.Configure<BancoOptions>(builder.Configuration.GetSection(BancoOptions.Seccion));
builder.Services.AddOrquestacion();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(
            e => e.Key,
            e => new { status = e.Value.Status.ToString(), e.Value.Description }),
    }),
});

// Procesa un expediente con un modelo, una condición (T1 sistema completo, T2 ablación sin RAG) y una repetición.
app.MapPost("/api/expedientes/{id:int}/procesar", async (
    int id, string modelo, Condicion? condicion, byte? repeticion,
    OrquestadorExpedientes orquestador, RegistroModelos registro, ILogger<Program> log, CancellationToken ct) =>
{
    if (registro.Buscar(modelo) is null)
        return Results.BadRequest(new { error = $"Modelo no registrado en config/models.json: {modelo}" });
    var c = condicion ?? Condicion.T1;
    if (c == Condicion.T0)
        return Results.BadRequest(new { error = "T0 es la línea base manual (ficha de cronometraje)" });
    var rep = repeticion ?? 1;
    if (rep is < 1 or > 5)
        return Results.BadRequest(new { error = "La repetición va de 1 a 5" });

    try
    {
        var r = await orquestador.ProcesarAsync(new SolicitudProcesamiento(id, modelo, c, rep), ct);
        if (r.Error is not null)
            log.LogWarning("Ejecución {IdEjecucion} del expediente {Id} terminó en Error: {Error}", r.IdEjecucion, id, r.Error);
        return Results.Ok(Resumenes.De(r.Ejecucion, r.Error));
    }
    catch (ExpedienteNoEncontradoException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// Estado, decisión y resolución de cada ejecución del expediente.
app.MapGet("/api/expedientes/{id:int}", async (int id, ReclamosDbContext db, CancellationToken ct) =>
{
    var expediente = await db.Expedientes.AsNoTracking().FirstOrDefaultAsync(e => e.IdExpediente == id, ct);
    if (expediente is null)
        return Results.NotFound();
    var ejecuciones = await Resumenes.Consulta(db).Where(e => e.IdExpediente == id).ToListAsync(ct);
    return Results.Ok(new
    {
        expediente.IdExpediente,
        expediente.CodigoOperacion,
        expediente.FechaIngreso,
        ejecuciones = ejecuciones.Select(e => Resumenes.De(e, null)),
    });
});

// Registros de ejecución (filtros opcionales por modelo y repetición).
app.MapGet("/api/ejecuciones", async (string? modelo, byte? repeticion, ReclamosDbContext db, CancellationToken ct) =>
{
    var q = Resumenes.Consulta(db);
    if (!string.IsNullOrWhiteSpace(modelo))
        q = q.Where(e => e.ModeloId == modelo);
    if (repeticion is { } r)
        q = q.Where(e => e.Repeticion == r);
    var lista = await q.OrderBy(e => e.IdEjecucion).ToListAsync(ct);
    return Results.Ok(lista.Select(e => Resumenes.De(e, null)));
});

app.Run();
