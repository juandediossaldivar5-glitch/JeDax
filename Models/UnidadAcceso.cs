namespace JeDax.Models;

public enum TipoMovimientoUnidad { Descarga, Carga }

public enum EstadoUnidad { Programada, EnPlanta, Salida, Retenida }

public class UnidadAcceso
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    // MKT fields
    public DateOnly Fecha { get; set; }
    public string Horario { get; set; } = string.Empty;
    public string ResponsableMkt { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string LineaTransportista { get; set; } = string.Empty;
    public string NombreOperador { get; set; } = string.Empty;
    public string Placas { get; set; } = string.Empty;
    public string NumeroCaja { get; set; } = string.Empty;
    public string TelefonoOperador { get; set; } = string.Empty;
    public TipoMovimientoUnidad TipoMovimiento { get; set; }

    // Operaciones fields (filled by Mlx)
    public EstadoUnidad Estatus { get; set; } = EstadoUnidad.Programada;
    public string? PersonaAcceso { get; set; }
    public TimeOnly? HoraIngreso { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? Comentario { get; set; }

    // Audit
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }

    // Nav
    public Tenant Tenant { get; set; } = null!;
}
