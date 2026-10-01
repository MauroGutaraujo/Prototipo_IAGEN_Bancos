using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Reclamos.Application.Agentes;
using Reclamos.Guardrails;
using Reclamos.Guardrails.Legal;
using Reclamos.Guardrails.Salida;
using Reclamos.Infrastructure;
using Reclamos.Infrastructure.Ocr;

var builder = WebApplication.CreateBuilder(args);

// Cadena de conexión: ConnectionStrings:Reclamos (user-secrets) o variable de entorno SQL_CONN.
var connectionString = builder.Configuration.GetConnectionString("Reclamos")
    ?? builder.Configuration["SQL_CONN"]
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión: define SQL_CONN o ConnectionStrings:Reclamos (user-secrets).");

builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.Seccion));
builder.Services.AddOcr();

// Agentes simbólicos con los parámetros de la tesis (sección "Guardrail").
// GuardrailSalida exige Guardrail:PlazoRespuesta: falla al resolverse si no está configurado.
builder.Services.Configure<GuardrailOptions>(builder.Configuration.GetSection(GuardrailOptions.Seccion));
builder.Services.AddSingleton<ILegalAgent>(sp =>
    new LegalAgent(new AgenteLegal(sp.GetRequiredService<IOptions<GuardrailOptions>>().Value)));
builder.Services.AddSingleton<IOutputGuardrail>(sp =>
    new OutputGuardrail(new GuardrailSalida(sp.GetRequiredService<IOptions<GuardrailOptions>>().Value)));

var app = builder.Build();

// Pinecone se agrega a /health en H5.
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

app.Run();
