namespace JeDax.Models;

public class Producto
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Código de parte. Ej: A005TV2281ZX</summary>
    public string Item { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    // Nav
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Case> Cases { get; set; } = [];
}
