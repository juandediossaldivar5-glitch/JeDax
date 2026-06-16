using JeDax.Data;
using JeDax.Models;

namespace JeDax.Services;

/// <summary>
/// Port de CaseGenerador.cs del sistema Laredo.
/// Genera bloques de códigos CASE secuenciales en formato CASE########.
/// </summary>
public class CaseGeneradorService
{
    private readonly AppDbContext _db;

    public CaseGeneradorService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Genera una lista de códigos CASE secuenciales.
    /// </summary>
    /// <param name="desde">Número de inicio (ej: 858000)</param>
    /// <param name="cantidad">Cuántos códigos generar</param>
    public static List<string> GenerarCodigos(int desde, int cantidad)
    {
        if (cantidad <= 0 || cantidad > 5000)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "Cantidad debe estar entre 1 y 5000.");

        var lista = new List<string>(cantidad);
        for (int i = 0; i < cantidad; i++)
            lista.Add($"CASE{(desde + i):D8}");

        return lista;
    }

    /// <summary>
    /// Valida cuáles de los códigos generados ya existen en BD.
    /// </summary>
    public async Task<HashSet<string>> FiltrarExistentesAsync(IEnumerable<string> codigos)
    {
        var lista = codigos.ToList();
        var existentes = _db.Cases
            .Where(c => lista.Contains(c.CaseCode))
            .Select(c => c.CaseCode);

        return [.. await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(existentes)];
    }

    /// <summary>
    /// Genera el contenido de texto para imprimir etiquetas (un código por línea).
    /// Coincide con el formato del sistema Laredo para impresoras Zebra/Brother.
    /// </summary>
    public static string GenerarTextoEtiquetas(IEnumerable<string> codigos)
        => string.Join("\n", codigos.Select(c => $"*{c}*\n{c}"));
}
