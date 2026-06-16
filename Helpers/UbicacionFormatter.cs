namespace JeDax.Helpers;

public static class UbicacionFormatter
{
    /// <summary>
    /// Devuelve los últimos 6 caracteres de la ubicación para mostrar.
    /// Si la ubicación es null/vacía, retorna "—".
    /// </summary>
    public static string Display(string? ubicacion)
    {
        if (string.IsNullOrWhiteSpace(ubicacion)) return "—";
        return ubicacion.Length <= 6 ? ubicacion : ubicacion[^6..];
    }
}