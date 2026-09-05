# SAKUMA PDF Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Subir un packing list PDF de SAKUMA → parsear → mostrar preview con CASECodes JeDax propuestos → confirmar → crear Vale de Entrada (Cerrado) + registrar CASEs en inventario.

**Architecture:** Flujo SSR en 3 pasos: (1) upload page `/sakuma-import`, (2) preview page `/sakuma-preview` que recibe el estado vía cookie JSON temporal (10 min), (3) endpoint confirm que crea Vale de Entrada + CASEs de forma atómica. El PDF se parsea con PdfPig (MIT). El número SAKUMA Invoice (`SKIA2602-AM1`) no pasa el regex MELXET de `ValeService`, así que el Vale se crea directamente en `SakumaImportService.ConfirmarImportAsync` (mismo patrón que `ValeImportService.CommitAsync`). Los CASEs se crean con `InventarioService.RegistrarEntradaAsync`. Los detalles del vale se marcan como procesados inmediatamente (el PDF es prueba de recibo). El vale se cierra al confirmar.

**Tech Stack:** .NET 10, PdfPig 0.1.9 (MIT), EF Core 9, SSR Blazor estático, form POST → minimal API.

**Spec:** Decisión en conversación: crear Vale de Entrada automáticamente + CASEs a partir del PDF SAKUMA packing list.

## Global Constraints

- .NET 10, sin `@rendermode InteractiveServer` — rompe proxy Railway
- Toda interactividad: `<form method="post">` → endpoint → `Results.Redirect(...)`
- Visual: Lime `#C8FF00` + dark `#0A0E0A`; `<Icon Name="..." />` en H1 y labels
- Sin nuevas tablas ni columnas EF — sin migraciones nuevas
- PdfPig ≥ 0.1.9 (MIT)
- CaseCode formato: `CASE{N:D8}` (8 dígitos, ej: `CASE00001234`)
- Invoice SAKUMA como Referencia del Vale (sin validación regex MELXET)

---

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---------|--------|-----------------|
| `JeDax.csproj` | Modificar | Agregar PdfPig |
| `Services/SakumaImportService.cs` | Crear | Parseo PDF + generación CASECodes + commit atómico |
| `Components/Pages/SakumaImport.razor` | Crear | Formulario upload PDF |
| `Components/Pages/SakumaPreview.razor` | Crear | Vista previa + form confirm con hidden inputs |
| `Program.cs` | Modificar | DI + 3 endpoints (upload, confirmar, cancelar) |
| `Components/Shared/NavMenu.razor` | Modificar | Link a /sakuma-import (solo Admin/Mlx) |
| `Components/Pages/Vales.razor` | Modificar | Banner de éxito para `ok=sakuma` |
| `Security/Permisos.cs` | Modificar | Agregar `PuedeImportarSakuma` |

---

### Task 1: PdfPig + SakumaImportService

**Files:**
- Modify: `JeDax.csproj`
- Create: `Services/SakumaImportService.cs`

**Interfaces:**
- Produce:
  - `record SakumaLinea(int SakumaCaseNo, string Item, string OrderNo, string Descripcion, int Qty)`
  - `record SakumaConfirmaLinea(string CaseCode, string Item, string Descripcion, int Qty)`
  - `class SakumaParseResult { string? InvoiceNo; List<SakumaLinea> Lineas; List<string> Errores; bool Exito }`
  - `SakumaImportService.ParsearPdfAsync(Stream) → Task<SakumaParseResult>`
  - `SakumaImportService.ObtenerSiguienteCaseNoAsync() → Task<int>`
  - `SakumaImportService.ConfirmarImportAsync(string invoiceNo, List<SakumaConfirmaLinea>, string usuario) → Task<Vale>`

- [ ] **Step 1: Agregar PdfPig a JeDax.csproj**

Dentro del `<ItemGroup>` de packages, agregar:
```xml
<PackageReference Include="PdfPig" Version="0.1.9" />
```

- [ ] **Step 2: Restore packages**

```bash
cd ~/Desktop/JeDax && dotnet restore
```
Expected: sin errores, PdfPig descargado.

- [ ] **Step 3: Crear Services/SakumaImportService.cs**

```csharp
using System.Text;
using System.Text.RegularExpressions;
using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace JeDax.Services;

public record SakumaLinea(int SakumaCaseNo, string Item, string OrderNo, string Descripcion, int Qty);
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

    // ── Parse ─────────────────────────────────────────────────────

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
            // Datos en página 2 (o última si hay más)
            var dataPage = doc.GetPage(Math.Min(2, totalPages));
            var lines = ExtractLines(dataPage);
            ParseDataRows(lines, result);
        }
        catch (Exception ex)
        {
            result.Errores.Add($"Error al leer el PDF: {ex.Message}");
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

    private static List<string> ExtractLines(UglyToad.PdfPig.Content.Page page)
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

    private static void ParseDataRows(List<string> lines, SakumaParseResult result)
    {
        // Patrón: "10 1W/C 404 PCS 4S0443 ／ 25X0393-00"
        // ／ puede ser U+FF0F (fullwidth slash) o ASCII /
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

    [GeneratedRegex(@"Invoice\s+No\.?\s*[:\.]?\s*([A-Z]{2,}[\d\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceNoRx();

    // Acepta ／ (U+FF0F fullwidth) y / (ASCII)
    [GeneratedRegex(@"^(\d+)\s+\d+W/C\s+(\d+)\s+PCS\s+([A-Z0-9]+)\s+[／/]\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex RowHeaderRx();
}
```

- [ ] **Step 4: Build para verificar compilación**

```bash
cd ~/Desktop/JeDax && dotnet build
```
Expected: Build succeeded, 0 errores. Si hay error de tipo en `UglyToad.PdfPig.Content.Page`, verificar que el namespace es correcto con `PdfPig` 0.1.9.

- [ ] **Step 5: Commit**

```bash
cd ~/Desktop/JeDax && git add JeDax.csproj Services/SakumaImportService.cs
git commit -m "feat: add SakumaImportService with PdfPig PDF parser"
```

---

### Task 2: Endpoints en Program.cs + permiso Permisos.cs

**Files:**
- Modify: `Security/Permisos.cs`
- Modify: `Program.cs`

**Interfaces:**
- Consumes: `SakumaImportService`, `CurrentUser`
- Produce:
  - `Permisos.PuedeImportarSakuma(RolUsuario) → bool`
  - `POST /api/sakuma/upload` → cookie `sakuma_preview` (JSON) → redirect `/sakuma-preview`
  - `POST /api/sakuma/confirmar` → `ConfirmarImportAsync` → redirect `/vales?ok=sakuma&ref=...`
  - `GET /api/sakuma/cancelar` → borra cookie → redirect `/sakuma-import`

- [ ] **Step 1: Agregar PuedeImportarSakuma a Permisos.cs**

Agregar después de `PuedeImportarValesSalida`:
```csharp
public static bool PuedeImportarSakuma(RolUsuario rol) =>
    rol is RolUsuario.Admin or RolUsuario.Mlx;
```

- [ ] **Step 2: Registrar SakumaImportService en DI en Program.cs**

Después de la línea `builder.Services.AddScoped<ValeImportService>();`:
```csharp
builder.Services.AddScoped<SakumaImportService>();
```

- [ ] **Step 3: Agregar endpoint POST /api/sakuma/upload en Program.cs**

Después del bloque `// ── SAKUMA PDF Import` (agregar esa sección después de `/api/vales/importar`):

```csharp
// ── SAKUMA PDF Import ─────────────────────────────────────────
app.MapPost("/api/sakuma/upload", async (HttpContext ctx, SakumaImportService svc, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    var file = f.Files.GetFile("archivo");
    if (file is null) return Results.Redirect("/sakuma-import?error=Sin+archivo");

    SakumaParseResult parsed;
    try
    {
        using var s = file.OpenReadStream();
        parsed = await svc.ParsearPdfAsync(s);
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/sakuma-import?error={Uri.EscapeDataString(ex.Message)}");
    }

    if (!parsed.Exito)
    {
        var err = string.Join("; ", parsed.Errores);
        return Results.Redirect($"/sakuma-import?error={Uri.EscapeDataString(err)}");
    }

    int siguiente = await svc.ObtenerSiguienteCaseNoAsync();
    var lineas = parsed.Lineas.Select((l, i) => new
    {
        caseCode = $"CASE{siguiente + i:D8}",
        l.Item,
        l.Descripcion,
        l.Qty
    }).ToList();

    var preview = System.Text.Json.JsonSerializer.Serialize(new
    {
        invoiceNo = parsed.InvoiceNo,
        lineas
    });

    ctx.Response.Cookies.Append("sakuma_preview", preview, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = ctx.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
    });

    return Results.Redirect("/sakuma-preview");
});
```

- [ ] **Step 4: Agregar endpoint POST /api/sakuma/confirmar**

Inmediatamente después del endpoint de upload:

```csharp
app.MapPost("/api/sakuma/confirmar", async (HttpContext ctx, SakumaImportService svc, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    var invoiceNo = f["invoiceNo"].ToString();
    var caseCodes = f["caseCode"].ToArray();
    var items = f["item"].ToArray();
    var descripciones = f["descripcion"].ToArray();
    var qtys = f["qty"].ToArray();

    if (caseCodes.Length == 0 || string.IsNullOrWhiteSpace(invoiceNo))
        return Results.Redirect("/sakuma-import?error=Datos+incompletos");

    var lineas = caseCodes.Select((code, i) => new SakumaConfirmaLinea(
        code, items[i], descripciones[i], int.Parse(qtys[i]))).ToList();

    try
    {
        await svc.ConfirmarImportAsync(invoiceNo, lineas, cu.User!.Username);
        ctx.Response.Cookies.Delete("sakuma_preview");
        return Results.Redirect($"/vales?ok=sakuma&ref={Uri.EscapeDataString(invoiceNo)}");
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/sakuma-preview?error={Uri.EscapeDataString(ex.Message)}");
    }
});
```

- [ ] **Step 5: Agregar endpoint GET /api/sakuma/cancelar**

```csharp
app.MapGet("/api/sakuma/cancelar", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("sakuma_preview");
    return Results.Redirect("/sakuma-import");
});
```

- [ ] **Step 6: Build**

```bash
cd ~/Desktop/JeDax && dotnet build
```
Expected: 0 errores.

- [ ] **Step 7: Commit**

```bash
cd ~/Desktop/JeDax && git add Security/Permisos.cs Program.cs
git commit -m "feat: add SAKUMA upload/confirm/cancel endpoints + PuedeImportarSakuma permission"
```

---

### Task 3: Páginas Razor (SakumaImport + SakumaPreview)

**Files:**
- Create: `Components/Pages/SakumaImport.razor`
- Create: `Components/Pages/SakumaPreview.razor`

**Interfaces:**
- Consumes: cookie `sakuma_preview` (JSON), query params `Error`
- Produce: páginas SSR con formularios que hacen POST a los endpoints de Task 2

- [ ] **Step 1: Crear Components/Pages/SakumaImport.razor**

```razor
@page "/sakuma-import"
@using JeDax.Components.Shared

<h1><Icon Name="upload" Size="32" /> Importar SAKUMA</h1>
<p style="color:var(--muted);margin-bottom:2rem;max-width:600px">
    Sube el packing list PDF de SAKUMA. El sistema generará el Vale de Entrada y los CASEs automáticamente.
</p>

@if (!string.IsNullOrEmpty(Error))
{
    <div class="alert-error"><Icon Name="info" Size="18" /> @Error</div>
}

<form method="post" action="/api/sakuma/upload" enctype="multipart/form-data" class="form-section">
    <label><Icon Name="upload" Size="12" /> Archivo PDF (packing list SAKUMA)</label>
    <input type="file" name="archivo" accept=".pdf" required class="input" />
    <button type="submit" class="btn-primary"><Icon Name="check" Size="16" /> Analizar PDF</button>
</form>

@code {
    [SupplyParameterFromQuery] public string? Error { get; set; }
}
```

- [ ] **Step 2: Crear Components/Pages/SakumaPreview.razor**

```razor
@page "/sakuma-preview"
@using System.Text.Json
@using JeDax.Components.Shared
@inject IHttpContextAccessor HttpCtx

@{
    var previewJson = HttpCtx.HttpContext?.Request.Cookies["sakuma_preview"];
    PreviewData? data = null;
    if (!string.IsNullOrEmpty(previewJson))
    {
        try { data = JsonSerializer.Deserialize<PreviewData>(previewJson, JsonOpts); } catch { }
    }
}

<h1><Icon Name="vales" Size="32" /> Confirmar importación SAKUMA</h1>
<p style="color:var(--muted);margin-bottom:2rem;max-width:600px">
    Verifica los CASEs antes de confirmar. Se creará un Vale de Entrada cerrado y los CASEs quedarán en inventario como <span class="badge badge-recibido">Recibido</span>.
</p>

@if (!string.IsNullOrEmpty(Error))
{
    <div class="alert-error"><Icon Name="info" Size="18" /> @Error</div>
}

@if (data is null)
{
    <div class="empty-state">
        <Icon Name="empty-box" Size="80" />
        <h3>Sin datos de importación</h3>
        <p>No hay una importación pendiente de confirmar.</p>
        <a href="/sakuma-import" class="btn-primary"><Icon Name="upload" Size="14" /> Subir PDF</a>
    </div>
}
else
{
    <div class="card" style="margin-bottom:1.5rem;display:flex;gap:2rem;align-items:center;flex-wrap:wrap">
        <span><span style="color:var(--muted)">Invoice No.</span> <strong>@data.InvoiceNo</strong></span>
        <span><span style="color:var(--muted)">Cajas</span> <strong>@data.Lineas.Count</strong></span>
        <span><span style="color:var(--muted)">Total PCS</span> <strong>@data.Lineas.Sum(l => l.Qty)</strong></span>
    </div>

    <div class="table-wrapper" style="margin-bottom:2rem;overflow-x:auto">
        <table class="table">
            <thead>
                <tr>
                    <th>CASE JeDax</th>
                    <th>Item</th>
                    <th>Descripción</th>
                    <th style="text-align:right">PCS</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var l in data.Lineas)
                {
                    <tr>
                        <td><code>@l.CaseCode</code></td>
                        <td>@l.Item</td>
                        <td style="font-size:.85rem;max-width:320px">@l.Descripcion</td>
                        <td style="text-align:right">@l.Qty</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>

    <form method="post" action="/api/sakuma/confirmar">
        <input type="hidden" name="invoiceNo" value="@data.InvoiceNo" />
        @foreach (var l in data.Lineas)
        {
            <input type="hidden" name="caseCode" value="@l.CaseCode" />
            <input type="hidden" name="item" value="@l.Item" />
            <input type="hidden" name="descripcion" value="@l.Descripcion" />
            <input type="hidden" name="qty" value="@l.Qty" />
        }
        <div style="display:flex;gap:1rem;flex-wrap:wrap">
            <button type="submit" class="btn-primary">
                <Icon Name="check" Size="16" /> Confirmar e importar (@data.Lineas.Count CASEs)
            </button>
            <a href="/api/sakuma/cancelar" class="btn-outline">Cancelar</a>
        </div>
    </form>
}

@code {
    [SupplyParameterFromQuery] public string? Error { get; set; }

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    private record PreviewLinea(string CaseCode, string Item, string Descripcion, int Qty);
    private record PreviewData(string InvoiceNo, List<PreviewLinea> Lineas);
}
```

- [ ] **Step 3: Build**

```bash
cd ~/Desktop/JeDax && dotnet build
```
Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
cd ~/Desktop/JeDax && git add Components/Pages/SakumaImport.razor Components/Pages/SakumaPreview.razor
git commit -m "feat: add SakumaImport and SakumaPreview pages"
```

---

### Task 4: NavMenu + banner Vales

**Files:**
- Modify: `Components/Shared/NavMenu.razor`
- Modify: `Components/Pages/Vales.razor`

**Interfaces:**
- Produce: link visible en nav para roles Admin/Mlx; banner éxito en `/vales?ok=sakuma`

- [ ] **Step 1: Agregar link SAKUMA en NavMenu.razor**

Después del bloque `@if (Permisos.PuedeVerImportarVales(...))`, agregar:
```razor
@if (Permisos.PuedeImportarSakuma(Current.User.Rol))
{
    <a href="/sakuma-import" class="nav-link"><Icon Name="upload" Size="16" /><span>SAKUMA</span></a>
}
```

- [ ] **Step 2: Agregar banner ok=sakuma en Vales.razor**

Después del bloque `@if (Ok == "1")` existente (línea ~15), agregar:
```razor
@if (Ok == "sakuma")
{
    <div class="alert-ok">
        <Icon Name="check" Size="18" /> Importación SAKUMA completada. Vale de Entrada creado y CASEs registrados.
        @if (!string.IsNullOrEmpty(Ref))
        {
            <span> Referencia: <strong>@Ref</strong></span>
        }
    </div>
}
```

También agregar el parámetro `Ref` en el bloque `@code`:
```csharp
[SupplyParameterFromQuery] public string? Ref { get; set; }
```

- [ ] **Step 3: Build**

```bash
cd ~/Desktop/JeDax && dotnet build
```
Expected: 0 errores.

- [ ] **Step 4: Commit**

```bash
cd ~/Desktop/JeDax && git add Components/Shared/NavMenu.razor Components/Pages/Vales.razor
git commit -m "feat: SAKUMA nav link + Vales success banner"
```

---

### Task 5: Smoke test manual con PDF real

**Files:** Sin cambios — solo verificación.

PDF de referencia: `~/Desktop/08. Packing Llist SKIA2602-AM1[55].pdf`
- Página 1: Invoice No. `SKIA2602-AM1`
- Página 2: 7 cajas numeradas 10–16, Items como `4S0443`, PCS entre 200–1000

- [ ] **Step 1: Arrancar la app**

```bash
cd ~/Desktop/JeDax && dotnet run
```

- [ ] **Step 2: Abrir `/sakuma-import` y subir el PDF**

Expected: redirect a `/sakuma-preview`.

- [ ] **Step 3: Verificar preview**

Checklist:
- Invoice No. muestra `SKIA2602-AM1`
- 7 filas con CASECodes secuenciales a partir del último en BD
- Items correctos (ej: `4S0443`)
- Qty correctas (ej: 404)
- Descripción incluye spec + `| OC: 25X0393-00`

Si algún campo está vacío o mal formateado: el PDF puede tener caracteres especiales en ／ o espacios distintos. Ajustar el regex `RowHeaderRx` en `SakumaImportService` según el texto extraído real.

**Cómo depurar el parseo sin la UI:** Agregar temporalmente un endpoint de diagnóstico o hacer un test en consola:
```csharp
// Diagnóstico: pegar esto en un endpoint GET /api/sakuma/debug
var doc = PdfDocument.Open(File.OpenRead("/Users/jd/Desktop/08. Packing Llist SKIA2602-AM1[55].pdf"));
var page2Lines = SakumaImportService.ExtractLinesPublic(doc.GetPage(2));
return Results.Ok(page2Lines);
```
(Hacer `ExtractLines` temporalmente `internal static` para accederlo, revertir después.)

- [ ] **Step 4: Confirmar importación**

Click "Confirmar e importar".
Expected: redirect a `/vales?ok=sakuma&ref=SKIA2602-AM1`, banner de éxito visible.

- [ ] **Step 5: Verificar en /stock**

Deben aparecer 7 nuevos CASEs con estado `Recibido` y sus Items.

- [ ] **Step 6: Verificar en /vales**

Vale de Entrada con:
- Referencia: `SKIA2602-AM1`
- Estado: `Cerrado`
- 7 detalles todos marcados Procesado

- [ ] **Step 7: Commit final si hubo ajustes de parseo**

```bash
cd ~/Desktop/JeDax && git add -A
git commit -m "fix: adjust SAKUMA PDF regex for real document format"
```
