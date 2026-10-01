using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reclamos.Infrastructure.Persistence;

/// <summary>
/// Usado solo por <c>dotnet ef</c>. Toma la cadena de la variable <c>SQL_CONN</c> o, si no
/// existe, de la línea <c>SQL_CONN=</c> del <c>.env</c> más cercano. Sin cadena, falla.
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

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión: define la variable de entorno SQL_CONN o agrégala al archivo .env.");
        }

        var options = new DbContextOptionsBuilder<ReclamosDbContext>()
            .UseSqlServer(conn)
            .Options;

        return new ReclamosDbContext(options);
    }
}
