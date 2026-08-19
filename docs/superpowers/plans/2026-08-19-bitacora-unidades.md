# Bitácora de Unidades MELX — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `/bitacora` module to JeDax that replaces the BITACORA MELX.numbers spreadsheet — Mkt registers incoming/outgoing trucks, Mlx completes the access fields, and the page warns when two units share the same scheduled time.

**Architecture:** SSR Razor component at `/bitacora` with form POST endpoints in Program.cs, a new `UnidadAcceso` EF Core model with tenant isolation, and inline row editing toggled via query param `?editar={id}` (no JS needed). Logo served as a static file from `wwwroot/`.

**Tech Stack:** .NET 10 Blazor SSR, EF Core 9, PostgreSQL (Railway) + SQLite (dev), C#

**Spec:** `docs/superpowers/specs/2026-08-19-bitacora-unidades-design.md`

## Global Constraints

- All files are in `~/Desktop/JeDax/`
- Tenant isolation: every new query filter must use `e.HasQueryFilter(x => x.TenantId == _tenant.TenantId)` in `AppDbContext.OnModelCreating`
- Postgres uses `EnsureCreatedAsync` (not migrations) — new table must also be created via raw SQL `CREATE TABLE IF NOT EXISTS` in Program.cs startup
- SQLite dev DB uses `MigrateAsync` — new migration required
- Form POST pattern: endpoint reads `ctx.Request.ReadFormAsync()`, redirects on success/error
- Auth: no new auth system — read `CurrentUser` (already DI-registered) for username and role
- Existing badge CSS pattern: class `badge badge-{statusname}` where statusname is the enum value lowercased
- No unit test project exists — verification is `dotnet run` + browser

---

### Task 1: Logo + Model

**Files:**
- Copy: `wwwroot/melx-logo.png` (from `/tmp/bitacora_extract/Data/image1-23.png`)
- Create: `Models/UnidadAcceso.cs`

**Interfaces:**
- Produces:
  - `enum TipoMovimientoUnidad { Descarga, Carga }`
  - `enum EstadoUnidad { Programada, EnPlanta, Salida, Retenida }`
  - `class UnidadAcceso` with all fields listed below

- [ ] **Step 1: Copy the MELX logo**

```bash
cp /tmp/bitacora_extract/Data/image1-23.png ~/Desktop/JeDax/wwwroot/melx-logo.png
```

Verify: `ls -lh ~/Desktop/JeDax/wwwroot/melx-logo.png` — should show ~10KB file.

- [ ] **Step 2: Create `Models/UnidadAcceso.cs`**

```csharp
namespace JeDax.Models;

public enum TipoMovimientoUnidad { Descarga, Carga }

public enum EstadoUnidad { Programada, EnPlanta, Salida, Retenida }

public class UnidadAcceso
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    // MKT fields
    public DateOnly Fecha { get; set; }
    public string Horario { get; set; } = string.Empty;
    public string ResponsableMkt { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public string LineaTransportista { get; set; } = string.Empty;
    public string NombreOperador { get; set; } = string.Empty;
    public string Placas { get; set; } = string.Empty;
    public string NumeroCaja { get; set; } = string.Empty;
    public string TelefonoOperador { get; set; } = string.Empty;
    public TipoMovimientoUnidad TipoMovimiento { get; set; }

    // Operaciones fields (filled by Mlx)
    public EstadoUnidad Estatus { get; set; } = EstadoUnidad.Programada;
    public string? PersonaAcceso { get; set; }
    public TimeOnly? HoraIngreso { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? Comentario { get; set; }

    // Audit
    public string CreadoPor { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }

    // Nav
    public Tenant Tenant { get; set; } = null!;
}
```

- [ ] **Step 3: Verify it compiles**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

### Task 2: Database wiring

**Files:**
- Modify: `Data/AppDbContext.cs`
- New migration (SQLite dev): run `dotnet ef migrations add AddUnidadAcceso`
- Modify: `Program.cs` lines ~55–78 (Postgres startup block)

**Interfaces:**
- Consumes: `UnidadAcceso`, `EstadoUnidad`, `TipoMovimientoUnidad` from Task 1
- Produces: `AppDbContext.UnidadAccesos` DbSet available to all endpoints and pages

- [ ] **Step 1: Add DbSet and model config to `AppDbContext.cs`**

After line 24 (`public DbSet<MovimientoHistorico> Movimientos => Set<MovimientoHistorico>();`), add:

```csharp
public DbSet<UnidadAcceso> UnidadAccesos => Set<UnidadAcceso>();
```

At the end of `OnModelCreating`, before the closing `}`, add:

```csharp
// ── UnidadAcceso ──────────────────────────────────────────────
mb.Entity<UnidadAcceso>(e =>
{
    e.HasQueryFilter(u => u.TenantId == _tenant.TenantId);
    e.HasIndex(u => new { u.TenantId, u.Fecha });
});
```

- [ ] **Step 2: Add Postgres table creation to `Program.cs` startup**

Inside the `if (usePostgres)` block in Program.cs, after `await db.Database.EnsureCreatedAsync();` (line ~72), add:

```csharp
await db.Database.ExecuteSqlRawAsync(@"
    CREATE TABLE IF NOT EXISTS ""UnidadAccesos"" (
        ""Id"" serial NOT NULL,
        ""TenantId"" integer NOT NULL,
        ""Fecha"" date NOT NULL,
        ""Horario"" text NOT NULL,
        ""ResponsableMkt"" text NOT NULL,
        ""Origen"" text NOT NULL,
        ""Destino"" text NOT NULL,
        ""LineaTransportista"" text NOT NULL,
        ""NombreOperador"" text NOT NULL,
        ""Placas"" text NOT NULL,
        ""NumeroCaja"" text NOT NULL,
        ""TelefonoOperador"" text NOT NULL,
        ""TipoMovimiento"" integer NOT NULL,
        ""Estatus"" integer NOT NULL DEFAULT 0,
        ""PersonaAcceso"" text,
        ""HoraIngreso"" time,
        ""HoraSalida"" time,
        ""Comentario"" text,
        ""CreadoPor"" text NOT NULL,
        ""CreadoEn"" timestamp with time zone NOT NULL,
        ""ActualizadoPor"" text,
        ""ActualizadoEn"" timestamp with time zone,
        CONSTRAINT ""PK_UnidadAccesos"" PRIMARY KEY (""Id"")
    )
");
```

- [ ] **Step 3: Create SQLite migration**

```bash
cd ~/Desktop/JeDax && dotnet ef migrations add AddUnidadAcceso
```

Expected: new file in `Migrations/` folder, no errors.

- [ ] **Step 4: Verify build + dev DB migration**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

Start the app once to apply migration:
```bash
cd ~/Desktop/JeDax && dotnet run &
sleep 5 && curl -s http://localhost:5000/health
kill %1
```

Expected: `JeDax OK`

---

### Task 3: Permissions + Truck icon

**Files:**
- Modify: `Security/Permisos.cs`
- Modify: `Components/Shared/Icon.razor`

**Interfaces:**
- Produces:
  - `Permisos.PuedeCrearBitacora(RolUsuario) → bool`
  - `Permisos.PuedeCompletarBitacora(RolUsuario) → bool`
  - `Permisos.PuedeVerBitacora(RolUsuario) → bool`
  - Icon name `"truck"` usable in `<Icon Name="truck" Size="16" />`

- [ ] **Step 1: Add permission methods to `Permisos.cs`**

Add these three methods to the `Permisos` class (after the last existing method):

```csharp
public static bool PuedeCrearBitacora(RolUsuario rol) =>
    rol is RolUsuario.Admin or RolUsuario.Mkt;

public static bool PuedeCompletarBitacora(RolUsuario rol) =>
    rol is RolUsuario.Admin or RolUsuario.Mlx;

public static bool PuedeVerBitacora(RolUsuario _) => true;
```

- [ ] **Step 2: Add truck icon to `Icon.razor`**

Inside the `@switch (Name)` block, before the closing `}`, add:

```csharp
case "truck":
    <svg width="@Size" height="@Size" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="icon">
        <rect x="1" y="3" width="15" height="13" /><polygon points="16 8 20 8 23 11 23 16 16 16 16 8" /><circle cx="5.5" cy="18.5" r="2.5" /><circle cx="18.5" cy="18.5" r="2.5" />
    </svg>
    break;
```

- [ ] **Step 3: Verify build**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

### Task 4: Badge CSS for unit statuses

**Files:**
- Modify: `wwwroot/css/app.css`

**Interfaces:**
- Produces: CSS classes `.badge-programada`, `.badge-enplanta`, `.badge-salida`, `.badge-retenida`

- [ ] **Step 1: Add badge styles to `app.css`**

After the last `.badge-*` rule block (around line 392), add:

```css
.badge-programada { background: rgba(255,255,255,.08); color: var(--muted); border-color: rgba(255,255,255,.15); }
.badge-enplanta   { background: rgba(46,204,113,.12); color: var(--green); border-color: rgba(46,204,113,.3); }
.badge-salida     { background: rgba(77,158,255,.12); color: var(--blue);  border-color: rgba(77,158,255,.3); }
.badge-retenida   { background: rgba(255,77,77,.12);  color: var(--red);   border-color: rgba(255,77,77,.3); }
```

---

### Task 5: POST endpoints in `Program.cs`

**Files:**
- Modify: `Program.cs` (add two endpoints before `app.MapRazorComponents`)

**Interfaces:**
- Consumes: `AppDbContext.UnidadAccesos`, `CurrentUser`, `Permisos.PuedeCrearBitacora`, `Permisos.PuedeCompletarBitacora`
- Consumes: `UnidadAcceso`, `EstadoUnidad`, `TipoMovimientoUnidad` from Task 1
- Produces:
  - `POST /api/bitacora/crear` — creates record, redirects to `/bitacora?warn=conflicto` if Fecha+Horario already taken
  - `POST /api/bitacora/actualizar/{id}` — updates Operaciones fields

- [ ] **Step 1: Add bitácora endpoints to `Program.cs`**

Before the line `app.MapRazorComponents<JeDax.Components.App>().DisableAntiforgery();`, add:

```csharp
// ── Bitácora de Unidades ──────────────────────────────────────
app.MapPost("/api/bitacora/crear", async (HttpContext ctx, AppDbContext db, CurrentUser cu) =>
{
    if (!Permisos.PuedeCrearBitacora(cu.User!.Rol))
        return Results.Forbid();

    var f = await ctx.Request.ReadFormAsync();
    var fecha = DateOnly.Parse(f["fecha"].ToString());
    var horario = f["horario"].ToString().Trim();

    var unidad = new UnidadAcceso
    {
        TenantId     = cu.User.TenantId,
        Fecha        = fecha,
        Horario      = horario,
        ResponsableMkt      = f["responsableMkt"].ToString().Trim(),
        Origen              = f["origen"].ToString().Trim(),
        Destino             = f["destino"].ToString().Trim(),
        LineaTransportista  = f["lineaTransportista"].ToString().Trim(),
        NombreOperador      = f["nombreOperador"].ToString().Trim(),
        Placas              = f["placas"].ToString().Trim(),
        NumeroCaja          = f["numeroCaja"].ToString().Trim(),
        TelefonoOperador    = f["telefonoOperador"].ToString().Trim(),
        TipoMovimiento      = Enum.Parse<TipoMovimientoUnidad>(f["tipoMovimiento"].ToString()),
        CreadoPor           = cu.User.Username,
        CreadoEn            = DateTime.UtcNow,
    };

    bool conflicto = await db.UnidadAccesos
        .AnyAsync(u => u.Fecha == fecha && u.Horario == horario);

    db.UnidadAccesos.Add(unidad);
    await db.SaveChangesAsync();

    var dest = $"/bitacora?fecha={fecha:yyyy-MM-dd}";
    if (conflicto) dest += "&warn=conflicto";
    return Results.Redirect(dest);
});

app.MapPost("/api/bitacora/actualizar/{id:int}", async (int id, HttpContext ctx, AppDbContext db, CurrentUser cu) =>
{
    if (!Permisos.PuedeCompletarBitacora(cu.User!.Rol))
        return Results.Forbid();

    var f = await ctx.Request.ReadFormAsync();
    var unidad = await db.UnidadAccesos.FindAsync(id);
    if (unidad is null) return Results.NotFound();

    unidad.Estatus       = Enum.Parse<EstadoUnidad>(f["estatus"].ToString());
    unidad.PersonaAcceso = f["personaAcceso"].ToString().Trim().NullIfEmpty();
    unidad.HoraIngreso   = TimeOnly.TryParse(f["horaIngreso"].ToString(), out var hi) ? hi : null;
    unidad.HoraSalida    = TimeOnly.TryParse(f["horaSalida"].ToString(), out var hs) ? hs : null;
    unidad.Comentario    = f["comentario"].ToString().Trim().NullIfEmpty();
    unidad.ActualizadoPor = cu.User.Username;
    unidad.ActualizadoEn  = DateTime.UtcNow;

    await db.SaveChangesAsync();

    var fecha = unidad.Fecha.ToString("yyyy-MM-dd");
    return Results.Redirect($"/bitacora?fecha={fecha}");
});
```

- [ ] **Step 2: Add `NullIfEmpty` string extension**

This is a tiny helper used by the actualizar endpoint. Add a file `Helpers/StringExtensions.cs`:

```csharp
namespace JeDax.Helpers;

public static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
```

Then add `using JeDax.Helpers;` at the top of `Program.cs` (after the existing using lines).

- [ ] **Step 3: Verify build**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

### Task 6: `Bitacora.razor` page

**Files:**
- Create: `Components/Pages/Bitacora.razor`

**Interfaces:**
- Consumes: `AppDbContext.UnidadAccesos`, `CurrentUser`, `Permisos.PuedeCrearBitacora`, `Permisos.PuedeCompletarBitacora`
- Consumes: `UnidadAcceso`, `EstadoUnidad`, `TipoMovimientoUnidad`
- Consumes: `<Icon>`, `<MainLayout>`
- Consumes: `wwwroot/melx-logo.png` served as `/melx-logo.png`
- Consumes: CSS classes `.badge-programada`, `.badge-enplanta`, `.badge-salida`, `.badge-retenida`

- [ ] **Step 1: Create `Components/Pages/Bitacora.razor`**

```razor
@page "/bitacora"
@using JeDax.Components.Shared
@using JeDax.Data
@using JeDax.Models
@using JeDax.Security
@using Microsoft.EntityFrameworkCore
@inject AppDbContext Db
@inject CurrentUser Current

<div style="display:flex;align-items:center;gap:1rem;margin-bottom:1rem">
    <img src="/melx-logo.png" alt="MELX" style="height:48px;border-radius:6px" />
    <h1 style="margin:0"><Icon Name="truck" Size="28" /> Control de Acceso de Unidades</h1>
</div>

@if (Warn == "conflicto")
{
    <div class="alert-warn">
        <Icon Name="info" Size="18" /> Ya existe otra unidad programada en el mismo horario. Se registró de todas formas.
    </div>
}

@* ── Filtros ── *@
<form method="get" class="toolbar" style="display:flex;gap:.75rem;align-items:center;flex-wrap:wrap;margin-bottom:1.5rem">
    <input type="date" name="fecha" value="@_fecha.ToString("yyyy-MM-dd")" class="input" style="width:auto" />
    <select name="estatus" class="input" style="width:auto">
        <option value="" selected="@(string.IsNullOrEmpty(Estatus))">Todos los estatus</option>
        @foreach (var e in Enum.GetValues<EstadoUnidad>())
        {
            <option value="@e" selected="@(Estatus == e.ToString())">@NombreEstatus(e)</option>
        }
    </select>
    <button type="submit" class="btn-outline"><Icon Name="search" Size="14" /> Filtrar</button>
    @if (Permisos.PuedeCrearBitacora(Current.User!.Rol))
    {
        <a href="#nueva" class="btn-primary" style="text-decoration:none;margin-left:auto">
            <Icon Name="truck" Size="14" /> Nueva unidad
        </a>
    }
</form>

@* ── Tabla ── *@
@if (_unidades.Count == 0)
{
    <div class="empty-state">
        <Icon Name="truck" Size="80" />
        <h3>Sin unidades</h3>
        <p>No hay unidades programadas para @_fecha.ToString("dd/MM/yyyy").</p>
    </div>
}
else
{
    <p style="color:var(--muted);font-size:12px;margin:.5rem 0">@_unidades.Count unidad(es) — @_fecha.ToString("dd/MM/yyyy")</p>
    <div style="overflow-x:auto">
        <table class="tabla" style="font-size:13px">
            <thead>
                <tr>
                    <th>Horario</th><th>Responsable MKT</th><th>Origen</th><th>Destino</th>
                    <th>Transportista</th><th>Operador</th><th>Placas</th><th>Caja</th>
                    <th>Teléfono</th><th>Tipo</th><th>Estatus</th><th>Acceso</th>
                    <th>Ingreso</th><th>Salida</th><th>Comentario</th>
                    @if (Permisos.PuedeCompletarBitacora(Current.User!.Rol))
                    {
                        <th></th>
                    }
                </tr>
            </thead>
            <tbody>
                @foreach (var u in _unidades)
                {
                    bool conflicto = _horariosConflicto.Contains(u.Horario);
                    bool editando = Editar == u.Id && Permisos.PuedeCompletarBitacora(Current.User!.Rol);

                    <tr class="@(conflicto ? "row-warn" : "")">
                        <td>
                            @if (conflicto)
                            {
                                <span title="Horario en conflicto">⚠ </span>
                            }
                            @u.Horario
                        </td>
                        <td>@u.ResponsableMkt</td>
                        <td>@u.Origen</td>
                        <td>@u.Destino</td>
                        <td>@u.LineaTransportista</td>
                        <td>@u.NombreOperador</td>
                        <td>@u.Placas</td>
                        <td>@u.NumeroCaja</td>
                        <td>@u.TelefonoOperador</td>
                        <td>@u.TipoMovimiento</td>
                        <td><span class="badge badge-@u.Estatus.ToString().ToLower()">@NombreEstatus(u.Estatus)</span></td>
                        <td>@u.PersonaAcceso</td>
                        <td>@u.HoraIngreso?.ToString("HH:mm")</td>
                        <td>@u.HoraSalida?.ToString("HH:mm")</td>
                        <td>@u.Comentario</td>
                        @if (Permisos.PuedeCompletarBitacora(Current.User!.Rol))
                        {
                            <td>
                                <a href="/bitacora?fecha=@_fecha.ToString("yyyy-MM-dd")&editar=@u.Id"
                                   class="btn-sm">Actualizar</a>
                            </td>
                        }
                    </tr>

                    @if (editando)
                    {
                        <tr style="background:rgba(200,255,0,.04)">
                            <td colspan="16" style="padding:1rem">
                                <form method="post" action="/api/bitacora/actualizar/@u.Id"
                                      style="display:flex;gap:.75rem;flex-wrap:wrap;align-items:flex-end">
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Estatus</label>
                                        <select name="estatus" class="input" style="width:auto">
                                            @foreach (var e in Enum.GetValues<EstadoUnidad>())
                                            {
                                                <option value="@e" selected="@(u.Estatus == e)">@NombreEstatus(e)</option>
                                            }
                                        </select>
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Persona que da acceso</label>
                                        <input name="personaAcceso" value="@u.PersonaAcceso" class="input" placeholder="Nombre" />
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Hora ingreso</label>
                                        <input type="time" name="horaIngreso" value="@u.HoraIngreso?.ToString("HH:mm")" class="input" style="width:auto" />
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Hora salida</label>
                                        <input type="time" name="horaSalida" value="@u.HoraSalida?.ToString("HH:mm")" class="input" style="width:auto" />
                                    </div>
                                    <div style="flex:1;min-width:180px">
                                        <label style="font-size:11px;color:var(--muted)">Comentario</label>
                                        <input name="comentario" value="@u.Comentario" class="input" placeholder="Observaciones" />
                                    </div>
                                    <button type="submit" class="btn-primary"><Icon Name="check" Size="14" /> Guardar</button>
                                    <a href="/bitacora?fecha=@_fecha.ToString("yyyy-MM-dd")" class="btn-outline">Cancelar</a>
                                </form>
                            </td>
                        </tr>
                    }
                }
            </tbody>
        </table>
    </div>
}

@* ── Formulario nueva unidad ── *@
@if (Permisos.PuedeCrearBitacora(Current.User!.Rol))
{
    <div id="nueva" style="margin-top:2.5rem">
        <h2 style="font-size:1rem;margin-bottom:1rem"><Icon Name="truck" Size="16" /> Registrar nueva unidad</h2>
        <form method="post" action="/api/bitacora/crear" class="form-section">
            <input type="hidden" name="fecha" value="@_fecha.ToString("yyyy-MM-dd")" />

            <label>Horario programado</label>
            <input name="horario" required class="input" placeholder="ej: 08:00–10:00" />

            <label>Responsable MKT</label>
            <input name="responsableMkt" required class="input" />

            <label>Origen</label>
            <input name="origen" required class="input" placeholder="Ciudad / planta de origen" />

            <label>Destino</label>
            <input name="destino" required class="input" placeholder="Ciudad / planta destino" />

            <label>Línea transportista</label>
            <input name="lineaTransportista" required class="input" />

            <label>Nombre del operador</label>
            <input name="nombreOperador" required class="input" />

            <label>Placas de la unidad</label>
            <input name="placas" required class="input" placeholder="ej: ABC-123-D" />

            <label>Número de caja</label>
            <input name="numeroCaja" required class="input" />

            <label>Teléfono del operador</label>
            <input name="telefonoOperador" required class="input" type="tel" />

            <label>Tipo de movimiento</label>
            <select name="tipoMovimiento" required class="input">
                <option value="Descarga">Descarga</option>
                <option value="Carga">Carga</option>
            </select>

            <button type="submit" class="btn-primary"><Icon Name="check" Size="16" /> Registrar unidad</button>
        </form>
    </div>
}

@code {
    [SupplyParameterFromQuery] public string? Fecha   { get; set; }
    [SupplyParameterFromQuery] public string? Estatus { get; set; }
    [SupplyParameterFromQuery] public int?    Editar  { get; set; }
    [SupplyParameterFromQuery] public string? Warn    { get; set; }

    DateOnly _fecha = DateOnly.FromDateTime(DateTime.Today);
    List<UnidadAcceso> _unidades = [];
    HashSet<string> _horariosConflicto = [];

    protected override async Task OnInitializedAsync()
    {
        if (DateOnly.TryParse(Fecha, out var f)) _fecha = f;

        var q = Db.UnidadAccesos.Where(u => u.Fecha == _fecha);

        if (Enum.TryParse<EstadoUnidad>(Estatus, out var est))
            q = q.Where(u => u.Estatus == est);

        _unidades = await q.OrderBy(u => u.Horario).ToListAsync();

        _horariosConflicto = _unidades
            .GroupBy(u => u.Horario)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();
    }

    static string NombreEstatus(EstadoUnidad e) => e switch
    {
        EstadoUnidad.Programada => "Programada",
        EstadoUnidad.EnPlanta   => "En planta",
        EstadoUnidad.Salida     => "Salida",
        EstadoUnidad.Retenida   => "Retenida",
        _                       => e.ToString()
    };
}
```

- [ ] **Step 2: Verify build**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

---

### Task 7: NavMenu link

**Files:**
- Modify: `Components/Shared/NavMenu.razor`

**Interfaces:**
- Consumes: `Permisos.PuedeVerBitacora(Current.User.Rol)`

- [ ] **Step 1: Add Bitácora link to `NavMenu.razor`**

After the `<a href="/historial" ...>Historial</a>` line, add:

```razor
@if (Permisos.PuedeVerBitacora(Current.User.Rol))
{
    <a href="/bitacora" class="nav-link"><Icon Name="truck" Size="16" /><span>Bitácora</span></a>
}
```

- [ ] **Step 2: Final build + smoke test**

```bash
cd ~/Desktop/JeDax && dotnet build -v q 2>&1 | tail -5
```

Expected: `Build succeeded.`

Start app and verify:
```bash
cd ~/Desktop/JeDax && dotnet run
```

Open `http://localhost:5000`, log in, navigate to `/bitacora`:
- Logo MELX visible top left
- Tabla vacía con mensaje "Sin unidades"
- Formulario "Registrar nueva unidad" visible (si rol Mkt/Admin)
- Registra una unidad → aparece en tabla con badge "Programada"
- Registra segunda con mismo horario → banner amarillo de conflicto + ⚠ en ambas filas
- Log in como Mlx → botón "Actualizar" visible → formulario inline → cambiar a "En planta" → badge verde
