using System.Text.RegularExpressions;
using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Services;

public partial class ValeService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public ValeService(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // ── Normalización (port de ValeRefUtil.cs) ────────────────────
    public static string NormalizarReferencia(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string s = raw.Trim().ToUpperInvariant()
            .Replace("–", "-").Replace("—", "-")
            .Replace("\u2019", "'").Replace("´", "'");

        s = RegexApostrofeBase().Replace(s, "$1$2-$3");
        s = RegexApostrofeSufijo().Replace(s, "-$1");

        return s;
    }

    public static bool EsReferenciaValida(string raw)
    {
        string s = NormalizarReferencia(raw);
        return RegexRefValida().IsMatch(s);
    }

    // ── Consultas ─────────────────────────────────────────────────
    public async Task<Vale?> ObtenerPorReferenciaAsync(string referencia)
    {
        string norm = NormalizarReferencia(referencia);
        return await _db.Vales
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Referencia == norm);
    }

    public async Task<List<Vale>> ListarAsync(TipoVale? tipo = null, EstadoVale? estado = null)
    {
        var q = _db.Vales.Include(v => v.Detalles).AsQueryable();
        if (tipo.HasValue) q = q.Where(v => v.Tipo == tipo.Value);
        if (estado.HasValue) q = q.Where(v => v.Estado == estado.Value);
        return await q.OrderByDescending(v => v.FechaCreacion).ToListAsync();
    }

    public async Task<Vale?> ObtenerPorIdAsync(int id)
        => await _db.Vales.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.Id == id);

    // ── Crear Vale ────────────────────────────────────────────────
    public async Task<Vale> CrearValeAsync(string referencia, TipoVale tipo, string creadoPor)
    {
        string norm = NormalizarReferencia(referencia);

        if (!EsReferenciaValida(norm))
            throw new InvalidOperationException($"Referencia inválida: {referencia}");

        if (await _db.Vales.AnyAsync(v => v.Referencia == norm))
            throw new InvalidOperationException($"La referencia {norm} ya existe.");

        var vale = new Vale
        {
            TenantId = _tenant.TenantId,
            Referencia = norm,
            Tipo = tipo,
            Estado = EstadoVale.Open,
            CreadoPor = creadoPor,
            FechaCreacion = DateTime.UtcNow
        };

        _db.Vales.Add(vale);
        await _db.SaveChangesAsync();
        return vale;
    }

    // ── Detalle ───────────────────────────────────────────────────
    public async Task AgregarDetalleAsync(int valeId, string caseCode, int qty,
        string? item = null, string? descripcion = null)
    {
        if (await _db.ValeDetalles.AnyAsync(d => d.ValeId == valeId
                && d.CaseCode == caseCode.Trim().ToUpperInvariant()))
            throw new InvalidOperationException($"CASE {caseCode} ya está en este vale.");

        _db.ValeDetalles.Add(new ValeDetalle
        {
            ValeId = valeId,
            CaseCode = caseCode.Trim().ToUpperInvariant(),
            Qty = qty,
            Item = item,
            Descripcion = descripcion
        });
        await _db.SaveChangesAsync();
    }

    public async Task EliminarDetalleAsync(int detalleId)
    {
        var d = await _db.ValeDetalles.FindAsync(detalleId)
            ?? throw new InvalidOperationException("Detalle no encontrado.");
        _db.ValeDetalles.Remove(d);
        await _db.SaveChangesAsync();
    }

    // ── Cerrar Vale ───────────────────────────────────────────────
    public async Task CerrarValeAsync(int valeId)
    {
        var vale = await _db.Vales.FindAsync(valeId)
            ?? throw new InvalidOperationException("Vale no encontrado.");
        if (vale.Estado != EstadoVale.Open) return;
        vale.Estado = EstadoVale.Cerrado;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Si todos los detalles del vale están procesados, lo cierra automáticamente.
    /// </summary>
    public async Task<bool> CerrarSiCompletoAsync(int valeId)
    {
        var vale = await _db.Vales
            .Include(v => v.Detalles)
            .FirstOrDefaultAsync(v => v.Id == valeId)
            ?? throw new InvalidOperationException("Vale no encontrado.");

        if (vale.Estado != EstadoVale.Open) return false;
        if (!vale.Detalles.Any()) return false;
        if (vale.Detalles.Any(d => !d.Procesado)) return false;

        vale.Estado = EstadoVale.Cerrado;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task CancelarValeAsync(int valeId)
    {
        var vale = await _db.Vales.FindAsync(valeId)
            ?? throw new InvalidOperationException("Vale no encontrado.");
        vale.Estado = EstadoVale.Cancelado;
        await _db.SaveChangesAsync();
    }

    // ── Regex (source-generated) ──────────────────────────────────
    [GeneratedRegex(@"^(MELXDOM|MELXET)(\d{2})'(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex RegexApostrofeBase();

    [GeneratedRegex(@"'([A-Z]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexApostrofeSufijo();

    [GeneratedRegex(@"^(MELXDOM|MELXET)\d{2}[-']\d{4}(['-]?[A-Z]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RegexRefValida();

    // ── Vale-first flow ──────────────────────────────────────────────

    /// <summary>
    /// Marca un detalle como procesado. Lanza si ya estaba procesado o no existe.
    /// </summary>
    public async Task<ValeDetalle> MarcarDetalleProcesadoAsync(int valeId, string caseCode, string usuario)
    {
        caseCode = caseCode.Trim().ToUpperInvariant();

        var detalle = await _db.ValeDetalles
            .FirstOrDefaultAsync(d => d.ValeId == valeId && d.CaseCode == caseCode)
            ?? throw new InvalidOperationException($"El CASE {caseCode} no está en este vale.");

        if (detalle.Procesado)
            throw new InvalidOperationException($"El CASE {caseCode} ya fue procesado.");

        detalle.Procesado = true;
        detalle.FechaProcesado = DateTime.UtcNow;
        detalle.UsuarioProcesado = usuario;

        await _db.SaveChangesAsync();
        return detalle;
    }

    /// <summary>
    /// Retorna (procesados, pendientes, total) para un vale.
    /// </summary>
    public async Task<(int procesados, int pendientes, int total)> ContarProgresoAsync(int valeId)
    {
        var detalles = await _db.ValeDetalles.Where(d => d.ValeId == valeId).ToListAsync();
        int procesados = detalles.Count(d => d.Procesado);
        return (procesados, detalles.Count - procesados, detalles.Count);
    }
}
