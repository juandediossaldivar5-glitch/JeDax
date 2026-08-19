using JeDax.Models;

namespace JeDax.Security;

public class SessionUser
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// Replica exacta de la lógica de SimpleAccess del sistema Laredo,
/// adaptada a los roles del enum RolUsuario.
/// </summary>
public static class Permisos
{
    public static bool PuedeUsarIngreso(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Lnk or RolUsuario.Laredo;

    public static bool PuedeRegistrarSalidas(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Lnk or RolUsuario.Laredo;

    public static bool PuedeVerStock(RolUsuario _) => true;

    public static bool PuedeImportarValesEntrada(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mlx;

    public static bool PuedeImportarValesSalida(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mkt;

    public static bool PuedeVerImportarVales(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mkt or RolUsuario.Mlx;

    public static bool PuedeAccederCaseGenerador(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Epq;

    public static bool EsAccesoLimitado(RolUsuario rol) =>
        rol is RolUsuario.Mkt or RolUsuario.Mlx;

    public static bool PuedeAdministrarUsuarios(RolUsuario rol) =>
        rol == RolUsuario.Admin;

    public static bool PuedeMoverInventario(RolUsuario _) => true;

    public static bool PuedeCrearBitacora(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mkt;

    public static bool PuedeCompletarBitacora(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mlx;

    public static bool PuedeVerBitacora(RolUsuario _) => true;
}
