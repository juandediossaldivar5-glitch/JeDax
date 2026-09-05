using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace JeDax.Services;

// ProposedCaseCode: null en PDF (se asigna secuencial desde DB), valor en Excel (BoxNo-LotNo)
public record SakumaLinea(int SakumaCaseNo, string Item, string OrderNo, string Descripcion, int Qty,
    string? ProposedCaseCode = null);
public record SakumaConfirmaLinea(string CaseCode, string Item, string Descripcion, int Qty);

public class SakumaParseResult
{
    public string? InvoiceNo { get; set; }
    public List<SakumaLinea> Lineas { get; set; } = [];
    public List<string> Errores { get; set; } = [];
    public bool Exito => !Errores.Any() && InvoiceNo is not null && Lineas.Count > 0;
}

public partial class SakumaImportService(AppDbContext db, TenantContext tenant, InventarioService inv)
{
    private readonly AppDbContext _db = db;
    private readonly TenantContext _tenant = tenant;
    private readonly InventarioService _inv = inv;

    // ── Parse PDF ─────────────────────────────────────────────────

    public Task<SakumaParseResult> ParsearPdfAsync(Stream stream)
    {
        var result = new SakumaParseResult();
        try
        {
            using var doc = PdfDocument.Open(stream);

            var page1Text = ExtractText(doc.GetPage(1));
            var invoiceMatch = InvoiceNoRx().Match(page1Text);
            if (!invoiceMatch.Success)
            {
                result.Errores.Add("No se encontró Invoice No. en la página 1 del PDF.");
                return Task.FromResult(result);
            }
            result.InvoiceNo = invoiceMatch.Groups[1].Value.Trim().ToUpperInvariant();

            int totalPages = doc.NumberOfPages;
            var dataPage = doc.GetPage(Math.Min(2, totalPages));
            var lines = ExtractLines(dataPage);
            ParsePdfDataRows(lines, result);
        }
        catch (Exception ex)
        {
            result.Errores.Add($"Error al leer el PDF: {ex.Message}");
        }
        return Task.FromResult(result);
    }

    // ── Parse Excel ───────────────────────────────────────────────

    // filename: nombre del archivo sin extensión → se usa como Referencia del Vale
    public Task<SakumaParseResult> ParsearExcelAsync(Stream stream, string filename)
    {
        var result = new SakumaParseResult();
        try
        {
            result.InvoiceNo = Path.GetFileNameWithoutExtension(filename)
                .Trim().ToUpperInvariant();

            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            int row = 2; // fila 1 = headers
            while (true)
            {
                var cellA = ws.Cell(row, 1);
                if (cellA.IsEmpty()) break;

                string boxNo = cellA.GetValue<string>().Trim();
                string material = ws.Cell(row, 2).GetValue<string>().Trim().ToUpperInvariant();
                string size = ws.Cell(row, 4).GetValue<string>().Trim();
                string lotNo = ws.Cell(row, 8).GetValue<string>().Trim().ToUpperInvariant();
                int pcs = ws.Cell(row, 10).TryGetValue<int>(out var q) ? q : 0;

                if (string.IsNullOrWhiteSpace(lotNo))
                {
                    result.Errores.Add($"Fila {row}: Lot No. (columna H) vacío.");
                    row++;
                    continue;
                }
                if (pcs <= 0)
                {
                    result.Errores.Add($"Fila {row}: PCS (columna J) inválido.");
                    row++;
                    continue;
                }

                string caseCode = $"{boxNo}-{lotNo}";
                result.Lineas.Add(new SakumaLinea(
                    SakumaCaseNo: int.TryParse(boxNo, out var bn) ? bn : 0,
                    Item: material,
                    OrderNo: lotNo,
                    Descripcion: size,
                    Qty: pcs,
                    ProposedCaseCode: caseCode));

                row++;
            }

            if (result.Lineas.Count == 0)
                result.Errores.Add("No se encontraron filas de datos en el Excel.");
        }
        catch (Exception ex)
        {
            result.Errores.Add($"Error al leer el Excel: {ex.Message}");
        }
        return Task.FromResult(result);
    }

    // ── DB helpers ────────────────────────────────────────────────

    public async Task<int> ObtenerSiguienteCaseNoAsync()
    {
        var ultimo = await _db.Cases
            .Where(c => c.CaseCode.StartsWith("CASE"))
            .MaxAsync(c => (string?)c.CaseCode);

        if (ultimo is null) return 1;
        return int.TryParse(ultimo["CASE".Length..], out var num) ? num + 1 : 1;
    }

    // ── Confirm ───────────────────────────────────────────────────

    public async Task<Vale> ConfirmarImportAsync(
        string invoiceNo, List<SakumaConfirmaLinea> lineas, string usuario)
    {
        invoiceNo = invoiceNo.Trim().ToUpperInvariant();

        if (await _db.Vales.AnyAsync(v => v.Referencia == invoiceNo))
            throw new InvalidOperationException($"La referencia {invoiceNo} ya existe en la base de datos.");

        var vale = new Vale
        {
            TenantId = _tenant.TenantId,
            Referencia = invoiceNo,
            Tipo = TipoVale.Entrada,
            Estado = EstadoVale.Open,
            CreadoPor = usuario,
            FechaCreacion = DateTime.UtcNow
        };
        _db.Vales.Add(vale);
        await _db.SaveChangesAsync();

        foreach (var linea in lineas)
        {
            await _inv.RegistrarEntradaAsync(
                linea.CaseCode, linea.Item, linea.Descripcion,
                linea.Qty, invoiceNo, usuario, _tenant.TenantId);

            _db.ValeDetalles.Add(new ValeDetalle
            {
                ValeId = vale.Id,
                CaseCode = linea.CaseCode,
                Item = linea.Item,
                Descripcion = linea.Descripcion,
                Qty = linea.Qty,
                Procesado = true,
                FechaProcesado = DateTime.UtcNow,
                UsuarioProcesado = usuario
            });
            await _db.SaveChangesAsync();
        }

        vale.Estado = EstadoVale.Cerrado;
        await _db.SaveChangesAsync();
        return vale;
    }

    // ── Private PDF helpers ───────────────────────────────────────

    private static string ExtractText(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left);
        return string.Join(" ", words.Select(w => w.Text));
    }

    internal static List<string> ExtractLines(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<string>();
        var currentLine = new List<string>();
        double currentY = double.MaxValue;

        foreach (var word in words)
        {
            double y = word.BoundingBox.Bottom;
            if (Math.Abs(y - currentY) > 3)
            {
                if (currentLine.Count > 0)
                    lines.Add(string.Join(" ", currentLine));
                currentLine = [word.Text];
                currentY = y;
            }
            else
            {
                currentLine.Add(word.Text);
            }
        }
        if (currentLine.Count > 0)
            lines.Add(string.Join(" ", currentLine));

        return lines;
    }

    private static void ParsePdfDataRows(List<string> lines, SakumaParseResult result)
    {
        var headerRx = RowHeaderRx();

        for (int i = 0; i < lines.Count; i++)
        {
            var m = headerRx.Match(lines[i]);
            if (!m.Success) continue;

            int caseNo = int.Parse(m.Groups[1].Value);
            int qty = int.Parse(m.Groups[2].Value);
            string item = m.Groups[3].Value.Trim().ToUpperInvariant();
            string orderNo = m.Groups[4].Value.Trim().ToUpperInvariant();

            var sb = new StringBuilder();
            if (i + 1 < lines.Count && !headerRx.IsMatch(lines[i + 1]))
                sb.Append(lines[i + 1].Trim());

            if (i + 2 < lines.Count && !headerRx.IsMatch(lines[i + 2]))
            {
                var specLine = lines[i + 2].Trim();
                var specPart = specLine.Split("KGS")[0].Trim();
                if (!string.IsNullOrWhiteSpace(specPart))
                    sb.Append(' ').Append(specPart);
            }

            string descripcion = $"{sb.ToString().Trim()} | OC: {orderNo}".Trim(' ', '|', ' ');
            result.Lineas.Add(new SakumaLinea(caseNo, item, orderNo, descripcion, qty));
        }

        if (result.Lineas.Count == 0)
            result.Errores.Add("No se encontraron líneas de cajas en la página 2. Verifica el formato del PDF.");
    }

    [GeneratedRegex(@"Invoice\s+No\.?\s*[:\.]?\s*([A-Z0-9][\w\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceNoRx();

    [GeneratedRegex(@"^PCS\s+(\d+)\s+\d+W/C\s+(\d+)\s+([A-Z0-9]+)\s+(\S+)\s+[／/]", RegexOptions.IgnoreCase)]
    private static partial Regex RowHeaderRx();
}
