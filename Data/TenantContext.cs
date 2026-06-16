namespace JeDax.Data;

/// <summary>
/// Inyectado como Scoped. Cada request/sesión de Blazor tiene su propio TenantId.
/// Se popula desde el middleware de sesión tras el login.
/// </summary>
public class TenantContext
{
    public int TenantId { get; private set; }
    public string TenantSlug { get; private set; } = string.Empty;
    public bool IsSet => TenantId > 0;

    public void Set(int tenantId, string slug)
    {
        TenantId = tenantId;
        TenantSlug = slug;
    }
}
