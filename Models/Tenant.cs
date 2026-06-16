namespace JeDax.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty; // jedax-laredo, jedax-cdmx, etc.
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    // Nav
    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<Producto> Productos { get; set; } = [];
    public ICollection<Case> Cases { get; set; } = [];
    public ICollection<Vale> Vales { get; set; } = [];
}
