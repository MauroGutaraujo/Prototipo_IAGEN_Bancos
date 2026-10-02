using Microsoft.EntityFrameworkCore;
using Reclamos.Domain.Entidades;
using Reclamos.Domain.Enums;
using Reclamos.Domain.Evaluacion;

namespace Reclamos.Infrastructure.Persistence;

public class ReclamosDbContext(DbContextOptions<ReclamosDbContext> options) : DbContext(options)
{
    public const string SchemaCore = "core";
    public const string SchemaApp = "app";
    public const string SchemaEval = "eval";

    // core
    public DbSet<ClienteSintetico> Clientes => Set<ClienteSintetico>();
    public DbSet<TipoCambio> TiposCambio => Set<TipoCambio>();
    public DbSet<Transaccion> Transacciones => Set<Transaccion>();

    // app
    public DbSet<Expediente> Expedientes => Set<Expediente>();
    public DbSet<Evidencia> Evidencias => Set<Evidencia>();
    public DbSet<CorridaBenchmark> Corridas => Set<CorridaBenchmark>();
    public DbSet<Ejecucion> Ejecuciones => Set<Ejecucion>();
    public DbSet<TransicionEstado> Transiciones => Set<TransicionEstado>();
    public DbSet<DecisionLegal> Decisiones => Set<DecisionLegal>();
    public DbSet<Recuperacion> Recuperaciones => Set<Recuperacion>();
    public DbSet<Resolucion> Resoluciones => Set<Resolucion>();
    public DbSet<RegistroManual> RegistrosManuales => Set<RegistroManual>();

    // eval: VerdadReferencia se mapea solo para que la migración cree la tabla.
    // A propósito no se expone como DbSet: la aplicación no la lee en tiempo de ejecución.

    protected override void OnModelCreating(ModelBuilder b)
    {
        ConfigurarCore(b);
        ConfigurarApp(b);
        ConfigurarEval(b);

        // Igual que db/schema.sql: FKs sin cascada (los registros son de auditoría).
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private static void ConfigurarCore(ModelBuilder b)
    {
        b.Entity<ClienteSintetico>(e =>
        {
            e.ToTable("ClienteSintetico", SchemaCore);
            e.HasKey(x => x.IdCliente);
            e.Property(x => x.AliasSeudonimo).HasMaxLength(60);
            e.Property(x => x.DispositivoRegistrado).HasMaxLength(60);
        });

        b.Entity<TipoCambio>(e =>
        {
            e.ToTable("TipoCambio", SchemaCore);
            e.HasKey(x => x.Fecha);
            e.Property(x => x.UsdPen).HasPrecision(9, 4);
        });

        b.Entity<Transaccion>(e =>
        {
            e.ToTable("Transaccion", SchemaCore);
            e.HasKey(x => x.CodigoOperacion);
            e.Property(x => x.CodigoOperacion).HasMaxLength(20);
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.Property(x => x.Moneda).HasConversion<string>().HasColumnType("char(3)");
            e.Property(x => x.Comercio).HasMaxLength(80);
            e.Property(x => x.Estado).HasConversion(new UpperSnakeEnumConverter<EstadoTransaccion>()).HasMaxLength(20);
            e.Property(x => x.Dispositivo).HasMaxLength(60);
            e.Property(x => x.IndicadorRiesgo).HasDefaultValue(false);
            e.HasOne(x => x.Cliente).WithMany(c => c.Transacciones).HasForeignKey(x => x.IdCliente);
        });
    }

    private static void ConfigurarApp(ModelBuilder b)
    {
        b.Entity<Expediente>(e =>
        {
            e.ToTable("Expediente", SchemaApp);
            e.HasKey(x => x.IdExpediente);
            e.Property(x => x.IdExpediente).ValueGeneratedNever();
            e.Property(x => x.CodigoOperacion).HasMaxLength(20);
            e.HasOne(x => x.Transaccion).WithMany().HasForeignKey(x => x.CodigoOperacion);
        });

        b.Entity<Evidencia>(e =>
        {
            e.ToTable("Evidencia", SchemaApp);
            e.HasKey(x => x.IdEvidencia);
            e.Property(x => x.RutaImagen).HasMaxLength(260);
            e.Property(x => x.PerturbacionAplicada).HasMaxLength(120);
            e.HasOne(x => x.Expediente).WithMany(x => x.Evidencias).HasForeignKey(x => x.IdExpediente);
        });

        b.Entity<CorridaBenchmark>(e =>
        {
            e.ToTable("CorridaBenchmark", SchemaApp);
            e.HasKey(x => x.IdCorrida);
            e.Property(x => x.Condicion).HasConversion<string>().HasColumnType("char(2)");
            e.Property(x => x.Notas).HasMaxLength(400);
        });

        b.Entity<Ejecucion>(e =>
        {
            e.ToTable("Ejecucion", SchemaApp);
            e.HasKey(x => x.IdEjecucion);
            e.Property(x => x.Condicion).HasConversion<string>().HasColumnType("char(2)");
            e.Property(x => x.ModeloId).HasMaxLength(60);
            e.Property(x => x.ModeloVersion).HasMaxLength(120);
            e.Property(x => x.PromptVersion).HasMaxLength(40);
            e.Property(x => x.InicioUtc).HasPrecision(3);
            e.Property(x => x.FinUtc).HasPrecision(3);
            e.Property(x => x.EstadoFinal).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IntencionPredicha).HasConversion(new UpperSnakeEnumConverter<Intencion>()).HasMaxLength(20);
            e.Property(x => x.MontoExtraido).HasPrecision(12, 2);
            e.Property(x => x.CodigoExtraido).HasMaxLength(20);
            e.Property(x => x.ConfMonto).HasPrecision(4, 3);
            e.Property(x => x.ConfFecha).HasPrecision(4, 3);
            e.Property(x => x.ConfCodigo).HasPrecision(4, 3);
            e.Property(x => x.Regeneraciones).HasDefaultValue((byte)0);
            e.Property(x => x.MotivoDerivacion).HasMaxLength(40);
            e.HasOne(x => x.Corrida).WithMany().HasForeignKey(x => x.IdCorrida);
            e.HasOne(x => x.Expediente).WithMany().HasForeignKey(x => x.IdExpediente);
            e.HasMany(x => x.Transiciones).WithOne().HasForeignKey(x => x.IdEjecucion);
            e.HasOne(x => x.DecisionLegal).WithOne().HasForeignKey<DecisionLegal>(x => x.IdEjecucion);
            e.HasOne(x => x.Recuperacion).WithOne().HasForeignKey<Recuperacion>(x => x.IdEjecucion);
            e.HasOne(x => x.Resolucion).WithOne().HasForeignKey<Resolucion>(x => x.IdEjecucion);
        });

        b.Entity<TransicionEstado>(e =>
        {
            e.ToTable("TransicionEstado", SchemaApp);
            e.HasKey(x => x.IdTransicion);
            e.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MarcaUtc).HasPrecision(3);
        });

        b.Entity<DecisionLegal>(e =>
        {
            e.ToTable("DecisionLegal", SchemaApp);
            e.HasKey(x => x.IdEjecucion);
            e.Property(x => x.Ruta).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.Regla).HasMaxLength(4);
            e.Property(x => x.Motivo).HasMaxLength(200);
        });

        b.Entity<Recuperacion>(e =>
        {
            e.ToTable("Recuperacion", SchemaApp);
            e.HasKey(x => x.IdEjecucion);
            e.Property(x => x.Calificacion).HasConversion<string>().HasMaxLength(12);
        });

        b.Entity<Resolucion>(e =>
        {
            e.ToTable("Resolucion", SchemaApp);
            e.HasKey(x => x.IdEjecucion);
        });

        b.Entity<RegistroManual>(e =>
        {
            e.ToTable("RegistroManual", SchemaApp);
            e.HasKey(x => x.IdRegistro);
            e.Property(x => x.CodigoAnalista).HasColumnType("char(2)");
            e.Property(x => x.TIngresoUtc).HasPrecision(3);
            e.Property(x => x.TFinalUtc).HasPrecision(3);
            e.Property(x => x.PausasMs).HasDefaultValue(0);
            e.Property(x => x.Intencion).HasConversion(new UpperSnakeEnumConverter<Intencion>()).HasMaxLength(20);
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.Property(x => x.Codigo).HasMaxLength(20);
            e.Property(x => x.Ruta).HasConversion<string>().HasMaxLength(15);
            e.HasOne(x => x.Expediente).WithMany().HasForeignKey(x => x.IdExpediente);
        });
    }

    private static void ConfigurarEval(ModelBuilder b)
    {
        b.Entity<VerdadReferencia>(e =>
        {
            e.ToTable("VerdadReferencia", SchemaEval);
            e.HasKey(x => x.IdExpediente);
            e.Property(x => x.IdExpediente).ValueGeneratedNever();
            e.Property(x => x.IntencionReal).HasConversion(new UpperSnakeEnumConverter<Intencion>()).HasMaxLength(20);
            e.Property(x => x.MontoReal).HasPrecision(12, 2);
            e.Property(x => x.MonedaReal).HasConversion<string>().HasColumnType("char(3)");
            e.Property(x => x.CodigoReal).HasMaxLength(20);
            e.Property(x => x.CamposLegibles).HasMaxLength(40);
            e.Property(x => x.RutaCorrecta).HasConversion<string>().HasMaxLength(15);
            e.Property(x => x.ReglaEsperada).HasMaxLength(4);
        });
    }
}
