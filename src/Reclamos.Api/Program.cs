using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Reclamos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Cadena de conexión: ConnectionStrings:Reclamos (user-secrets) o variable de entorno SQL_CONN.
var connectionString = builder.Configuration.GetConnectionString("Reclamos")
    ?? builder.Configuration["SQL_CONN"]
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión: define SQL_CONN o ConnectionStrings:Reclamos (user-secrets).");

builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

// Pinecone y Tesseract se agregan a /health en H4/H5.
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
