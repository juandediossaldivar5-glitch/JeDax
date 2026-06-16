namespace JeDax.Models;

public enum TipoMovimiento
{
    Entrada,
    Salida,
    Ubicacion,
    Ajuste
}

public class MovimientoHistorico
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public string CaseCode { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public TipoMovimiento Tipo { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public string? Ubicacion { get; set; }
    public int Qty { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
