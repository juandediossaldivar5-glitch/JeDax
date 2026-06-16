using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Services;

public class InventarioService
{
    private readonly AppDbContext _db;

    public InventarioService(AppDbContext db)
    {
        _db = db;
    }

    // ── Consultas ─────────────────────────────────────────────────

    public async Task<Case?> BuscarCasePorCodigoAsync(string caseCode)
        => await _db.Cases
            .Include(c => c.Producto)
            .FirstOrDefaultAsync(c => c.CaseCode == caseCode.Trim().ToUpperInvariant());

    public async Task<List<Case>> ObtenerStockActivoAsync()
        => await _db.Cases
            .Include(c => c.Producto)
            .Where(c => c.Estado != EstadoCase.Salida)
            .OrderBy(c => c.Producto.Item)
            .ThenBy(c => c.Ubicacion)
            .ToListAsync();

    public async Task<List<Case>> ObtenerHistorialAsync(int pagina = 1, int tamano = 50)
        => await _db.Cases
            .Include(c => c.Producto)
            .OrderByDescending(c => c.FechaRegistro)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync();

    public async Task<List<MovimientoHistorico>> ObtenerMovimientosAsync()
        => await _db.Movimientos
            .OrderByDescending(m => m.Fecha)
            .ToListAsync();

    public async Task<ResumenStock> ObtenerResumenAsync()
    {
        var cases = await _db.Cases
            .Where(c => c.Estado != EstadoCase.Salida)
            .ToListAsync();

        return new ResumenStock
        {
            TotalCasesEnStock = cases.Count,
            TotalPiezasEnStock = cases.Sum(c => c.QtyActual),
            CasesUbicados = cases.Count(c => c.Estado == EstadoCase.Ubicado),
            CasesSinUbicar = cases.Count(c => c.Estado == EstadoCase.Recibido)
        };
    }

    // ── Entrada ───────────────────────────────────────────────────

    /// <summary>
    /// Registra la entrada de un CASE al almacén.
    /// Si el producto (Item) no existe, lo crea.
    /// </summary>
    public async Task<Case> RegistrarEntradaAsync(
        string caseCode, string item, string descripcion,
        int qty, string refEntrada, string usuario, int tenantId)
    {
        caseCode = caseCode.Trim().ToUpperInvariant();

        if (await _db.Cases.AnyAsync(c => c.CaseCode == caseCode))
            throw new InvalidOperationException($"El CASE {caseCode} ya está registrado.");

        // Obtener o crear producto
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Item == item)
            ?? await CrearProductoAsync(item, descripcion, tenantId);

        var @case = new Case
        {
            TenantId = tenantId,
            CaseCode = caseCode,
            Estado = EstadoCase.Recibido,
            ProductoId = producto.Id,
            RefEntrada = refEntrada,
            QtyInicial = qty,
            QtyActual = qty,
            UsuarioRegistro = usuario,
            FechaRegistro = DateTime.UtcNow
        };

        _db.Cases.Add(@case);

        _db.Movimientos.Add(new MovimientoHistorico
        {
            TenantId = tenantId,
            CaseCode = caseCode,
            Item = item,
            Descripcion = descripcion,
            Tipo = TipoMovimiento.Entrada,
            Referencia = refEntrada,
            Qty = qty,
            Usuario = usuario,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return @case;
    }

    // ── Ubicar ────────────────────────────────────────────────────

    public async Task UbicarCaseAsync(string caseCode, string ubicacion, string usuario)
    {
        var @case = await _db.Cases
            .Include(c => c.Producto)
            .FirstOrDefaultAsync(c => c.CaseCode == caseCode)
            ?? throw new InvalidOperationException($"CASE {caseCode} no encontrado.");

        @case.Ubicacion = ubicacion.Trim().ToUpperInvariant();
        @case.Estado = EstadoCase.Ubicado;
        @case.FechaUbicacion = DateTime.UtcNow;

        _db.Movimientos.Add(new MovimientoHistorico
        {
            TenantId = @case.TenantId,
            CaseCode = caseCode,
            Item = @case.Producto.Item,
            Descripcion = @case.Producto.Descripcion,
            Tipo = TipoMovimiento.Ubicacion,
            Referencia = ubicacion,
            Ubicacion = ubicacion,
            Qty = @case.QtyActual,
            Usuario = usuario,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ── Salida ────────────────────────────────────────────────────

    /// <summary>
    /// Marca un CASE como salida y descuenta la cantidad.
    /// qty = 0 significa salida total.
    /// </summary>
    public async Task RegistrarSalidaAsync(
        string caseCode, string refSalida, int qty, string usuario)
    {
        var @case = await _db.Cases
            .Include(c => c.Producto)
            .FirstOrDefaultAsync(c => c.CaseCode == caseCode)
            ?? throw new InvalidOperationException($"CASE {caseCode} no encontrado.");

        if (@case.Estado == EstadoCase.Salida)
            throw new InvalidOperationException($"CASE {caseCode} ya fue dado de baja.");

        int qtySalida = qty == 0 ? @case.QtyActual : qty;

        if (qtySalida > @case.QtyActual)
            throw new InvalidOperationException(
                $"Cantidad solicitada ({qtySalida}) supera stock actual ({@case.QtyActual}).");

        @case.QtyActual -= qtySalida;
        @case.RefSalida = refSalida;
        @case.FechaSalida = DateTime.UtcNow;

        if (@case.QtyActual == 0)
            @case.Estado = EstadoCase.Salida;

        _db.Movimientos.Add(new MovimientoHistorico
        {
            TenantId = @case.TenantId,
            CaseCode = caseCode,
            Item = @case.Producto.Item,
            Descripcion = @case.Producto.Descripcion,
            Tipo = TipoMovimiento.Salida,
            Referencia = refSalida,
            Ubicacion = @case.Ubicacion,
            Qty = qtySalida,
            Usuario = usuario,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    // ── Privados ──────────────────────────────────────────────────

    private async Task<Producto> CrearProductoAsync(string item, string descripcion, int tenantId)
    {
        var p = new Producto
        {
            TenantId = tenantId,
            Item = item.Trim().ToUpperInvariant(),
            Descripcion = descripcion.Trim()
        };
        _db.Productos.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    // ── Mover / Ubicar ────────────────────────────────────────────

    public static bool EsUbicacionValida(string ubicacion)
    {
        if (string.IsNullOrWhiteSpace(ubicacion)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
            ubicacion.Trim().ToUpperInvariant(),
            @"^[A-Z0-9]{4}/[A-Z0-9]{3}/\d{6}$");
    }

    /// <summary>
    /// Mueve un CASE a una nueva ubicación. También sirve para ubicar
    /// CASEs recién ingresados (sin ubicación previa).
    /// </summary>
    public async Task MoverCaseAsync(string caseCode, string nuevaUbicacion, string usuario)
    {
        caseCode = caseCode.Trim().ToUpperInvariant();
        nuevaUbicacion = nuevaUbicacion.Trim().ToUpperInvariant();

        if (!EsUbicacionValida(nuevaUbicacion))
            throw new InvalidOperationException($"Formato de ubicación inválido: {nuevaUbicacion}. Esperado: XXXX/XXX/NNNNNN");

        var @case = await _db.Cases
            .Include(c => c.Producto)
            .FirstOrDefaultAsync(c => c.CaseCode == caseCode)
            ?? throw new InvalidOperationException($"CASE {caseCode} no encontrado.");

        if (@case.Estado == EstadoCase.Salida)
            throw new InvalidOperationException($"CASE {caseCode} ya fue dado de baja.");

        string? ubicacionAnterior = @case.Ubicacion;

        if (ubicacionAnterior == nuevaUbicacion)
            throw new InvalidOperationException($"El CASE {caseCode} ya está en {nuevaUbicacion}.");

        @case.Ubicacion = nuevaUbicacion;
        @case.Estado = EstadoCase.Ubicado;
        @case.FechaUbicacion = DateTime.UtcNow;

        _db.Movimientos.Add(new MovimientoHistorico
        {
            TenantId = @case.TenantId,
            CaseCode = caseCode,
            Item = @case.Producto.Item,
            Descripcion = @case.Producto.Descripcion,
            Tipo = TipoMovimiento.Ubicacion,
            Referencia = string.IsNullOrEmpty(ubicacionAnterior) ? "INICIAL" : ubicacionAnterior,
            Ubicacion = nuevaUbicacion,
            Qty = @case.QtyActual,
            Usuario = usuario,
            Fecha = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Lista las ubicaciones existentes (distintas) para autocomplete.
    /// </summary>
    public async Task<List<string>> ObtenerUbicacionesExistentesAsync()
        => await _db.Cases
            .Where(c => c.Ubicacion != null && c.Estado != EstadoCase.Salida)
            .Select(c => c.Ubicacion!)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();

    /// <summary>
    /// Lista los CASEs en stock para dropdown (Admin).
    /// </summary>
    public async Task<List<Case>> ObtenerCasesParaSeleccionAsync()
        => await _db.Cases
            .Include(c => c.Producto)
            .Where(c => c.Estado != EstadoCase.Salida)
            .OrderBy(c => c.CaseCode)
            .ToListAsync();
}

public record ResumenStock
{
    public int TotalCasesEnStock { get; init; }
    public int TotalPiezasEnStock { get; init; }
    public int CasesUbicados { get; init; }
    public int CasesSinUbicar { get; init; }
}
