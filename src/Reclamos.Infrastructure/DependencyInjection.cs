using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reclamos.Infrastructure.Persistence;

namespace Reclamos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReclamosDbContext>(o => o.UseSqlServer(connectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<ReclamosDbContext>("sqlserver");

        return services;
    }
}
