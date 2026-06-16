namespace JeDax.Models;

public enum RolUsuario
{
    Admin,
    Operador,
    Lnk,
    Mkt,
    Mlx,
    Epq,
    Laredo
}

public class Usuario
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    // Nav
    public Tenant Tenant { get; set; } = null!;
}
