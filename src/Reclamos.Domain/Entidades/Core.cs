using Reclamos.Domain.Enums;

namespace Reclamos.Domain.Entidades;

// Esquema `core`: núcleo transaccional simulado (solo datos sintéticos).

public class ClienteSintetico
{
    public int IdCliente { get; set; }
    public required string AliasSeudonimo { get; set; }
    public required string DispositivoRegistrado { get; set; }

    public ICollection<Transaccion> Transacciones { get; set; } = [];
}

public class TipoCambio
{
    public DateOnly Fecha { get; set; }
    public decimal UsdPen { get; set; }
}

public class Transaccion
{
    public required string CodigoOperacion { get; set; }
    public int IdCliente { get; set; }
    public decimal Monto { get; set; }
    public Moneda Moneda { get; set; }
    public DateTime FechaHora { get; set; }
    public required string Comercio { get; set; }
    public EstadoTransaccion Estado { get; set; }
    public bool AutenticacionReforzada { get; set; }
    public string? Dispositivo { get; set; }
    public bool IndicadorRiesgo { get; set; }

    public ClienteSintetico? Cliente { get; set; }
}
