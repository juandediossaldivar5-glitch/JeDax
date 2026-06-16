using ClosedXML.Excel;
using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Services;

public class ValeImportService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public ValeImportService(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public class ImportResult
    {
        public bool Exito => !Errores.Any();
        public List<string> Errores { get; set; } = [];
        public string? Referencia { get; set; }
        public TipoVale Tipo { get; set; }
        public List<ValeDetalle> Detalles { get; set; } = [];
        public int TotalPiezas => Detalles.Sum(d => d.Qty);
    }

    /// <summary>
    /// Parsea un archivo Excel de vale (formato LANDESK-MEAX).
    /// Valida pero NO persiste. Para persistir usar CommitAsync().
    /// </summary>
    public async Task<ImportResult> ParsearAsync(Stream excelStream)
    {
        var result = new ImportResult();

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheets.First();

        // Referencia: AT7 (con asteriscos *MELXET26-XXXX*)
        var refRaw = ws.Cell("AT7").GetString().Trim();
        if (string.IsNullOrWhiteSpace(refRaw))
        {
            result.Errores.Add("No se puede importar: falta referencia del vale (celda AT7).");
            return result;
        }
        result.Referencia = refRaw.Trim('*', ' ').ToUpperInvariant();

        // Tipo: AV11 ("VALE DE ENTRADA" o "VALE DE SALIDA")
        var tipoRaw = ws.Cell("AV11").GetString().Trim().ToUpperInvariant();
        if (tipoRaw.Contains("ENTRADA")) result.Tipo = TipoVale.Entrada;
        else if (tipoRaw.Contains("SALIDA")) result.Tipo = TipoVale.Salida;
        else
        {
            result.Errores.Add($"No se puede importar: tipo de vale inválido o vacío (celda AV11): '{tipoRaw}'.");
            return result;
        }

        // Validar referencia única en BD
        if (await _db.Vales.AnyAsync(v => v.Referencia == result.Referencia))
        {
            result.Errores.Add($"No se puede importar: la referencia {result.Referencia} ya existe en la base de datos.");
            return result;
        }

        // Detalles: desde fila 36 hacia abajo, hasta encontrar CASE vacío
        // Columnas: E=CASE, U=ITEM, AJ=NOMBRE, BE=QTY
        int row = 36;
        var caseCodesEnArchivo = new HashSet<string>();

        while (true)
        {
            var caseCode = ws.Cell(row, "E").GetString().Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(caseCode)) break;

            var item = ws.Cell(row, "U").GetString().Trim().ToUpperInvariant();
            var descripcion = ws.Cell(row, "AJ").GetString().Trim();
            var qtyRaw = ws.Cell(row, "BE").GetString().Trim();

            // Validaciones por fila
            if (string.IsNullOrWhiteSpace(item))
                result.Errores.Add($"Fila {row}: el CASE {caseCode} no tiene Item (columna U).");

            if (string.IsNullOrWhiteSpace(qtyRaw))
                result.Errores.Add($"Fila {row}: el CASE {caseCode} no tiene cantidad (columna BE).");

            if (!int.TryParse(qtyRaw, out int qty) || qty <= 0)
                result.Errores.Add($"Fila {row}: cantidad inválida para CASE {caseCode}: '{qtyRaw}'.");

            if (!caseCodesEnArchivo.Add(caseCode))
                result.Errores.Add($"Fila {row}: CASE {caseCode} está duplicado en el archivo.");

            result.Detalles.Add(new ValeDetalle
            {
                CaseCode = caseCode,
                Item = string.IsNullOrWhiteSpace(item) ? null : item,
                Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion,
                Qty = int.TryParse(qtyRaw, out var q) ? q : 0,
                Procesado = false
            });

            row++;
        }

        if (!result.Detalles.Any())
        {
            result.Errores.Add("No se puede importar: el archivo no contiene detalles (filas vacías desde la 36).");
            return result;
        }

        // Validaciones cruzadas contra inventario
        var codes = caseCodesEnArchivo.ToList();
        var casesEnBd = await _db.Cases
            .Where(c => codes.Contains(c.CaseCode))
            .ToListAsync();

        foreach (var code in codes)
        {
            var existente = casesEnBd.FirstOrDefault(c => c.CaseCode == code);

            if (result.Tipo == TipoVale.Entrada)
            {
                if (existente is not null && existente.Estado != EstadoCase.Salida)
                    result.Errores.Add($"CASE {code}: ya está en inventario (no se puede ingresar de nuevo).");
            }
            else // Salida
            {
                if (existente is null)
                    result.Errores.Add($"CASE {code}: no existe en inventario (no se puede dar de baja).");
                else if (existente.Estado == EstadoCase.Salida)
                    result.Errores.Add($"CASE {code}: ya fue dado de baja anteriormente.");
            }
        }

        return result;
    }

    /// <summary>
    /// Persiste el resultado de un parseo exitoso.
    /// </summary>
    public async Task<Vale> CommitAsync(ImportResult parsed, string creadoPor)
    {
        if (!parsed.Exito)
            throw new InvalidOperationException("No se puede confirmar un parseo con errores.");

        var vale = new Vale
        {
            TenantId = _tenant.TenantId,
            Referencia = parsed.Referencia!,
            Tipo = parsed.Tipo,
            Estado = EstadoVale.Open,
            CreadoPor = creadoPor,
            FechaCreacion = DateTime.UtcNow,
            Detalles = parsed.Detalles
        };

        _db.Vales.Add(vale);
        await _db.SaveChangesAsync();
        return vale;
    }
}