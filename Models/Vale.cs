namespace JeDax.Models;

public enum TipoVale
{
    Entrada,
    Salida
}

public enum EstadoVale
{
    Open,
    Cerrado,
    Cancelado
}

public class Vale
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Ej: MELXET26-0126-A</summary>
    public string Referencia { get; set; } = string.Empty;

    public TipoVale Tipo { get; set; }
    public EstadoVale Estado { get; set; } = EstadoVale.Open;

    public string CreadoPor { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Nav
    public Tenant Tenant { get; set; } = null!;
    public ICollection<ValeDetalle> Detalles { get; set; } = [];
}

public class ValeDetalle
{
    public int Id { get; set; }
    public int ValeId { get; set; }

    public string CaseCode { get; set; } = string.Empty;

    /// <summary>Solo en Entrada: item y descripción del case</summary>
    public string? Item { get; set; }
    public string? Descripcion { get; set; }

    public int Qty { get; set; }

    // Tracking de procesamiento
    public bool Procesado { get; set; }
    public DateTime? FechaProcesado { get; set; }
    public string? UsuarioProcesado { get; set; }

    // Nav
    public Vale Vale { get; set; } = null!;
}