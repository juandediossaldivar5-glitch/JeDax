using ClosedXML.Excel;
using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Services;

public class ExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Genera un xlsx con el stock activo, fiel al formato BaseDeDatos.xlsx</summary>
    public async Task<byte[]> ExportarStockAsync()
    {
        var cases = await _db.Cases
            .Include(c => c.Producto)
            .Where(c => c.Estado != EstadoCase.Salida)
            .OrderBy(c => c.Producto.Item)
            .ThenBy(c => c.Ubicacion)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("CASE Escaneados");

        // Headers
        string[] headers = ["INVOICE", "ITEM", "DESCRIPCION", "CASE", "CASE SCAN",
                             "DATE CASE", "UBICACIÓN", "DATE UBICACIÓN", "USUARIO",
                             "REF.SALIDA", "REF.ENTRADA", "QTY INICIAL", "QTY ACTUAL", "QTY SALIDAS"];

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1a1d27");
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.FromHtml("#00e5ff");
        }

        int row = 2;
        foreach (var c in cases)
        {
            ws.Cell(row, 1).Value = c.RefEntrada ?? "";
            ws.Cell(row, 2).Value = c.Producto.Item;
            ws.Cell(row, 3).Value = c.Producto.Descripcion;
            ws.Cell(row, 4).Value = c.CaseCode;
            ws.Cell(row, 5).Value = c.Estado.ToString().ToUpperInvariant();
            ws.Cell(row, 6).Value = c.FechaRegistro.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 7).Value = c.Ubicacion ?? "";
            ws.Cell(row, 8).Value = c.FechaUbicacion?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ws.Cell(row, 9).Value = c.UsuarioRegistro;
            ws.Cell(row, 10).Value = c.RefSalida ?? "";
            ws.Cell(row, 11).Value = c.RefEntrada ?? "";
            ws.Cell(row, 12).Value = c.QtyInicial;
            ws.Cell(row, 13).Value = c.QtyActual;
            ws.Cell(row, 14).Value = c.QtySalidas;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Genera xlsx con el historial de movimientos</summary>
    public async Task<byte[]> ExportarHistorialAsync()
    {
        var movs = await _db.Movimientos
            .OrderByDescending(m => m.Fecha)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Movimientos");

        string[] headers = ["FECHA", "TIPO", "CASE", "ITEM", "DESCRIPCION",
                             "QTY", "REFERENCIA", "UBICACIÓN", "USUARIO"];

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        int row = 2;
        foreach (var m in movs)
        {
            ws.Cell(row, 1).Value = m.Fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 2).Value = m.Tipo.ToString().ToUpperInvariant();
            ws.Cell(row, 3).Value = m.CaseCode;
            ws.Cell(row, 4).Value = m.Item;
            ws.Cell(row, 5).Value = m.Descripcion;
            ws.Cell(row, 6).Value = m.Qty;
            ws.Cell(row, 7).Value = m.Referencia;
            ws.Cell(row, 8).Value = m.Ubicacion ?? "";
            ws.Cell(row, 9).Value = m.Usuario;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Genera xlsx de vales con sus detalles</summary>
    public async Task<byte[]> ExportarValesAsync()
    {
        var vales = await _db.Vales
            .Include(v => v.Detalles)
            .OrderByDescending(v => v.FechaCreacion)
            .ToListAsync();

        using var wb = new XLWorkbook();

        // Hoja Vales
        var wsVales = wb.Worksheets.Add("Vales");
        string[] hVales = ["REF.SALIDA", "CREADO POR", "FECHA", "ESTADO", "TIPO"];
        for (int i = 0; i < hVales.Length; i++)
        {
            wsVales.Cell(1, i + 1).Value = hVales[i];
            wsVales.Cell(1, i + 1).Style.Font.Bold = true;
        }
        int r = 2;
        foreach (var v in vales)
        {
            wsVales.Cell(r, 1).Value = v.Referencia;
            wsVales.Cell(r, 2).Value = v.CreadoPor;
            wsVales.Cell(r, 3).Value = v.FechaCreacion.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            wsVales.Cell(r, 4).Value = v.Estado.ToString().ToUpperInvariant();
            wsVales.Cell(r, 5).Value = v.Tipo.ToString().ToUpperInvariant();
            r++;
        }

        // Hoja detalles salida
        var wsSal = wb.Worksheets.Add("Vale CASES");
        string[] hSal = ["REF.SALIDA", "CASE", "QTY"];
        for (int i = 0; i < hSal.Length; i++) { wsSal.Cell(1, i + 1).Value = hSal[i]; wsSal.Cell(1, i + 1).Style.Font.Bold = true; }
        int rSal = 2;
        foreach (var v in vales.Where(v => v.Tipo == TipoVale.Salida))
            foreach (var d in v.Detalles)
            {
                wsSal.Cell(rSal, 1).Value = v.Referencia;
                wsSal.Cell(rSal, 2).Value = d.CaseCode;
                wsSal.Cell(rSal, 3).Value = d.Qty;
                rSal++;
            }

        // Hoja detalles entrada
        var wsEnt = wb.Worksheets.Add("Vale ENT CASES");
        string[] hEnt = ["REF.ENTRADA", "CASE", "ITEM", "DESCRIPCION", "QTY"];
        for (int i = 0; i < hEnt.Length; i++) { wsEnt.Cell(1, i + 1).Value = hEnt[i]; wsEnt.Cell(1, i + 1).Style.Font.Bold = true; }
        int rEnt = 2;
        foreach (var v in vales.Where(v => v.Tipo == TipoVale.Entrada))
            foreach (var d in v.Detalles)
            {
                wsEnt.Cell(rEnt, 1).Value = v.Referencia;
                wsEnt.Cell(rEnt, 2).Value = d.CaseCode;
                wsEnt.Cell(rEnt, 3).Value = d.Item ?? "";
                wsEnt.Cell(rEnt, 4).Value = d.Descripcion ?? "";
                wsEnt.Cell(rEnt, 5).Value = d.Qty;
                rEnt++;
            }

        wsVales.Columns().AdjustToContents();
        wsSal.Columns().AdjustToContents();
        wsEnt.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
