using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reclamos.Infrastructure.Persistence;

/// <summary>
/// Usado solo por <c>dotnet ef</c>. Toma la cadena de <c>SQL_CONN</c>; si no existe, usa una
/// cadena local sin credenciales (generar una migración no abre conexión).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReclamosDbContext>
{
    public ReclamosDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("SQL_CONN");

        if (string.IsNullOrWhiteSpace(conn))
        {
            var dir = Directory.GetCurrentDirectory();
            while (dir != null && !File.Exists(Path.Combine(dir, ".env")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }

            if (dir != null)
            {
                var envFile = Path.Combine(dir, ".env");
                foreach (var line in File.ReadAllLines(envFile))
                {
                    if (line.StartsWith("SQL_CONN=", StringComparison.OrdinalIgnoreCase))
                    {
                        conn = line["SQL_CONN=".Length..].Trim();
                        break;
                    }
                }
            }
        }

        conn ??= "Server=localhost,1433;Database=ReclamosTesis;User Id=sa;Password=CambiaEstaClave_2026!;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<ReclamosDbContext>()
            .UseSqlServer(conn)
            .Options;

        return new ReclamosDbContext(options);
    }
}
