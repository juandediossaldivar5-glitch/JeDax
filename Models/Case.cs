namespace JeDax.Models;

public enum EstadoCase
{
    Recibido,
    Ubicado,
    Salida
}

public class Case
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Código de barras del case. Ej: CASE00858214</summary>
    public string CaseCode { get; set; } = string.Empty;

    public EstadoCase Estado { get; set; } = EstadoCase.Recibido;

    /// <summary>Código de ubicación en almacén. Ej: 020201</summary>
    public string? Ubicacion { get; set; }

    public int ProductoId { get; set; }

    /// <summary>Referencia del vale de entrada que originó este case</summary>
    public string? RefEntrada { get; set; }

    /// <summary>Referencia del vale de salida que consumió este case</summary>
    public string? RefSalida { get; set; }

    public int QtyInicial { get; set; }
    public int QtyActual { get; set; }
    public int QtySalidas => QtyInicial - QtyActual;

    public string UsuarioRegistro { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime? FechaUbicacion { get; set; }
    public DateTime? FechaSalida { get; set; }

    // Nav
    public Tenant Tenant { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
