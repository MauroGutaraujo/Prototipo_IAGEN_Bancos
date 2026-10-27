using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reclamos.Application.Ficha;
using Reclamos.Application.Rag;
using Reclamos.Infrastructure.Persistence;
using Reclamos.Infrastructure.Rag;

namespace Reclamos.Api;

/// <summary>Endpoints de la ficha de cronometraje T0 (SPEC §5). Página estática en /ficha/.</summary>
public static class FichaEndpoints
{
    public sealed record Login(string Codigo, string Pin);

    public static IServiceCollection AddFichaAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "ficha_t0";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.ExpireTimeSpan = TimeSpan.FromHours(10);
                // API: sin redirecciones, solo códigos de estado.
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            });
        services.AddAuthorization();
        return services;
    }

    public static void MapFicha(this WebApplication app)
    {
        app.MapGet("/ficha", () => Results.Redirect("/ficha/index.html"));

        var api = app.MapGroup("/ficha/api");

        api.MapPost("/login", async (Login l, ServicioFicha ficha, HttpContext http) =>
        {
            if (!ficha.Autenticar(l.Codigo, l.Pin))
                return Results.Unauthorized();
            var identidad = new ClaimsIdentity([new Claim(ClaimTypes.Name, l.Codigo)], CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad));
            return Results.Ok(new { analista = l.Codigo });
        });

        var privado = api.MapGroup("").RequireAuthorization();

        privado.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        });

        privado.MapGet("/casos", (ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) => Ejecutar(() => f.ListarAsync(A(u), ct)));
        privado.MapPost("/casos/{id:int}/abrir", (int id, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
            Ejecutar(() => f.AbrirAsync(A(u), id, ct)));
        privado.MapPost("/casos/{id:int}/latido", (int id, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
            Ejecutar(() => f.LatidoAsync(A(u), id, ct)));
        privado.MapPost("/casos/{id:int}/pausa", (int id, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
            Ejecutar(() => f.PausarAsync(A(u), id, ct)));
        privado.MapPost("/casos/{id:int}/reanudar", (int id, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
            Ejecutar(() => f.ReanudarAsync(A(u), id, ct)));
        privado.MapPost("/casos/{id:int}/enviar", (int id, RespuestaAnalista r, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
            Ejecutar(() => f.EnviarAsync(A(u), id, r, ct)));

        privado.MapGet("/casos/{id:int}/voucher", async (int id, ServicioFicha f, ClaimsPrincipal u, CancellationToken ct) =>
        {
            try
            {
                return Results.File(await f.RutaVoucherAsync(A(u), id, ct), "image/jpeg");
            }
            catch (FichaException ex)
            {
                return Error(ex);
            }
        });

        // Buscador del core por código: la transacción y los demás cargos del mismo cliente (lo que consulta el Agente Legal).
        privado.MapGet("/core/transacciones", async (string codigo, ReclamosDbContext db, CancellationToken ct) =>
        {
            var tx = await db.Transacciones.AsNoTracking().FirstOrDefaultAsync(t => t.CodigoOperacion == codigo.Trim().ToUpper(), ct);
            if (tx is null)
                return Results.NotFound(new { error = $"No existe la operación {codigo}" });
            var cliente = await db.Clientes.AsNoTracking().FirstAsync(c => c.IdCliente == tx.IdCliente, ct);
            var movimientos = await db.Transacciones.AsNoTracking()
                .Where(t => t.IdCliente == tx.IdCliente)
                .OrderBy(t => t.FechaHora)
                .Select(t => new { t.CodigoOperacion, t.Monto, t.Moneda, t.FechaHora, t.Comercio, t.Estado, t.AutenticacionReforzada, t.Dispositivo, t.IndicadorRiesgo })
                .ToListAsync(ct);
            var dia = DateOnly.FromDateTime(tx.FechaHora);
            var tipoCambio = await db.TiposCambio.AsNoTracking().Where(t => t.Fecha == dia).Select(t => (decimal?)t.UsdPen).FirstOrDefaultAsync(ct);
            return Results.Ok(new { operacion = tx.CodigoOperacion, cliente.DispositivoRegistrado, tipoCambio, movimientos });
        });

        // Política interna simulada (del corpus versionado); la normativa oficial se enlaza a sus fuentes.
        privado.MapGet("/normas/politica", (IOptions<RagOptions> rag) =>
        {
            var corpus = CorpusNormativo.Cargar(rag.Value.CorpusPath, rag.Value.CorpusSha256);
            return Results.Ok(corpus.Fragmentos.Where(f => f.Tipo == "simulada").Select(f => new { f.Id, f.Texto }));
        });
    }

    private static string A(ClaimsPrincipal u) => u.Identity!.Name!;

    private static async Task<IResult> Ejecutar<T>(Func<Task<T>> accion)
    {
        try
        {
            return Results.Ok(await accion());
        }
        catch (FichaException ex)
        {
            return Error(ex);
        }
    }

    private static async Task<IResult> Ejecutar(Func<Task> accion)
    {
        try
        {
            await accion();
            return Results.NoContent();
        }
        catch (FichaException ex)
        {
            return Error(ex);
        }
    }

    private static IResult Error(FichaException ex) => ex.Error switch
    {
        ErrorFicha.NoAsignado => Results.NotFound(new { error = ex.Message }),
        _ => Results.Conflict(new { error = ex.Message, codigo = ex.Error.ToString() }),
    };
}
