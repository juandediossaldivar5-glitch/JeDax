# Bitácora MELX — Standalone App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone .NET 10 SSR app at `~/Desktop/bitacora-melx` that replaces the BITACORA MELX.numbers spreadsheet, with 4 roles and kitting-web-style auth, deployed as a separate Railway service sharing the existing PostgreSQL database.

**Architecture:** New .NET 10 Blazor SSR project, same pattern as `~/Desktop/kitting-web`. Auth is one shared password per role stored in Railway env vars — no user table. Single PostgreSQL table `MelxUnidades` on the shared Railway Postgres (no multi-tenant). After the new app is built, the Bitácora module is removed from JeDax.

**Tech Stack:** .NET 10, Blazor SSR, EF Core 9, Npgsql, PostgreSQL (shared Railway instance)

**Spec:** `docs/superpowers/specs/2026-08-19-bitacora-melx-standalone-design.md`

## Global Constraints

- .NET 10 target framework (`net10.0`)
- Namespace: `BitacoraMelx` (root namespace in csproj)
- Table name: `MelxUnidades` (exact — shared Postgres, must not conflict with JeDax tables)
- Cookie name: `melx_session`
- Env var pattern: `MELX_PASS_ADM`, `MELX_PASS_MKT`, `MELX_PASS_OPE`, `MELX_PASS_MELX`
- Config key pattern: `MelxPass:ADM`, `MelxPass:MKT`, `MelxPass:OPE`, `MelxPass:MELX`
- Roles enum: `RolMelx { ADM, MKT, OPE, MELX }` (exact casing)
- DB connection: `UsePostgres` boolean in appsettings; if true use PG* env vars; if false use `ConnectionStrings:Default`
- No migrations — raw SQL `CREATE TABLE IF NOT EXISTS` at startup (same pattern as JeDax Postgres path)
- Logo source: `/private/tmp/bitacora_extract/Data/image1-23.png` → `wwwroot/melx-logo.png`
- Project directory: `~/Desktop/bitacora-melx`

---

### Task 1: Project scaffold + security layer

**Files:**
- Create: `~/Desktop/bitacora-melx/bitacora-melx.csproj`
- Create: `~/Desktop/bitacora-melx/appsettings.json`
- Create: `~/Desktop/bitacora-melx/appsettings.Production.json`
- Create: `~/Desktop/bitacora-melx/Security/RolMelx.cs`
- Create: `~/Desktop/bitacora-melx/Security/SessionUser.cs`
- Create: `~/Desktop/bitacora-melx/Security/AuthService.cs`
- Create: `~/Desktop/bitacora-melx/Security/CurrentUser.cs`
- Create: `~/Desktop/bitacora-melx/Security/Permisos.cs`

**Interfaces:**
- Produces: `RolMelx` enum, `SessionUser` class, `AuthService.Login(string, string) → SessionUser?`, `CurrentUser.User`, `Permisos.PuedeRegistrar`, `Permisos.PuedeCompletarOperaciones`, `Permisos.PuedeVer` — all later tasks use these.

- [ ] **Step 1: Create project directory and scaffold**

```bash
cd ~/Desktop
dotnet new web -n bitacora-melx --framework net10.0
cd bitacora-melx
git init
echo "bin/\nobj/\n*.user\n.env" > .gitignore
```

- [ ] **Step 2: Replace the generated .csproj**

Delete the generated `bitacora-melx.csproj` and write this one:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>BitacoraMelx</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.1">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.3" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write appsettings.json (local dev)**

```json
{
  "UsePostgres": false,
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=bitacora_melx;Username=postgres;Password=postgres"
  },
  "MelxPass": {
    "ADM": "admin123",
    "MKT": "mkt2026",
    "OPE": "ope2026",
    "MELX": "melx2026"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  }
}
```

- [ ] **Step 4: Write appsettings.Production.json (Railway)**

```json
{
  "UsePostgres": true
}
```

- [ ] **Step 5: Write Security/RolMelx.cs**

```csharp
namespace BitacoraMelx.Security;

public enum RolMelx { ADM, MKT, OPE, MELX }
```

- [ ] **Step 6: Write Security/SessionUser.cs**

```csharp
namespace BitacoraMelx.Security;

public class SessionUser
{
    public string Usuario { get; set; } = "";
    public RolMelx Rol { get; set; }
    public DateTime LoginAt { get; set; }
}
```

- [ ] **Step 7: Write Security/AuthService.cs**

```csharp
namespace BitacoraMelx.Security;

public class AuthService(IConfiguration cfg)
{
    public SessionUser? Login(string usuario, string password)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            return null;

        foreach (var rol in Enum.GetValues<RolMelx>())
        {
            var expected = cfg[$"MelxPass:{rol}"]
                           ?? Environment.GetEnvironmentVariable($"MELX_PASS_{rol}");
            if (!string.IsNullOrEmpty(expected) && password == expected)
                return new SessionUser { Usuario = usuario.Trim(), Rol = rol, LoginAt = DateTime.UtcNow };
        }
        return null;
    }
}
```

- [ ] **Step 8: Write Security/CurrentUser.cs**

```csharp
namespace BitacoraMelx.Security;

public class CurrentUser
{
    public SessionUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public void Set(SessionUser u) => User = u;
}
```

- [ ] **Step 9: Write Security/Permisos.cs**

```csharp
namespace BitacoraMelx.Security;

public static class Permisos
{
    public static bool PuedeVer(RolMelx _) => true;
    public static bool PuedeRegistrar(RolMelx r) =>
        r is RolMelx.ADM or RolMelx.MKT or RolMelx.MELX;
    public static bool PuedeCompletarOperaciones(RolMelx r) =>
        r is RolMelx.ADM or RolMelx.OPE;
}
```

- [ ] **Step 10: Verify build**

```bash
cd ~/Desktop/bitacora-melx
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Commit**

```bash
cd ~/Desktop/bitacora-melx
git add -A
git commit -m "feat: scaffold project + security layer"
```

---

### Task 2: Data model + DbContext + table creation at startup

**Files:**
- Create: `~/Desktop/bitacora-melx/Models/MelxUnidad.cs`
- Create: `~/Desktop/bitacora-melx/Data/AppDbContext.cs`
- Create: `~/Desktop/bitacora-melx/Program.cs` (initial version — DB wiring only)

**Interfaces:**
- Consumes: `RolMelx`, `SessionUser`, `CurrentUser`, `AuthService`, `Permisos` from Task 1
- Produces: `AppDbContext.MelxUnidades` DbSet, table `"MelxUnidades"` in Postgres — used by Tasks 4 and 5

- [ ] **Step 1: Write Models/MelxUnidad.cs**

```csharp
namespace BitacoraMelx.Models;

public class MelxUnidad
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string Horario { get; set; } = "";
    public DateTime HoraRegistro { get; set; } = DateTime.UtcNow;
    public string ResponsableMkt { get; set; } = "";
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public string LineaTransportista { get; set; } = "";
    public string NombreOperador { get; set; } = "";
    public string Placas { get; set; } = "";
    public string NumeroCaja { get; set; } = "";
    public string TelefonoOperador { get; set; } = "";
    public string TipoMovimiento { get; set; } = "Descarga";
    public string Estatus { get; set; } = "Programada";
    public string? PersonaAcceso { get; set; }
    public TimeOnly? HoraIngreso { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public string? Comentario { get; set; }
    public string CreadoPor { get; set; } = "";
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoEn { get; set; }
}
```

- [ ] **Step 2: Write Data/AppDbContext.cs**

```csharp
using BitacoraMelx.Models;
using Microsoft.EntityFrameworkCore;

namespace BitacoraMelx.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MelxUnidad> MelxUnidades => Set<MelxUnidad>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<MelxUnidad>(e =>
        {
            e.ToTable("MelxUnidades");
            e.HasIndex(u => new { u.Fecha, u.Horario });
        });
    }
}
```

- [ ] **Step 3: Write Program.cs (DB wiring + table creation)**

Delete the generated `Program.cs` and write:

```csharp
using System.Text.Json;
using BitacoraMelx.Data;
using BitacoraMelx.Security;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AuthService>();

bool usePostgres = builder.Configuration.GetValue<bool>("UsePostgres");
string connStr = usePostgres
    ? string.Format("Host={0};Port={1};Database={2};Username={3};Password={4}",
        Environment.GetEnvironmentVariable("PGHOST"),
        Environment.GetEnvironmentVariable("PGPORT"),
        Environment.GetEnvironmentVariable("PGDATABASE"),
        Environment.GetEnvironmentVariable("PGUSER"),
        Environment.GetEnvironmentVariable("PGPASSWORD"))
    : builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddDbContext<AppDbContext>(opt => opt
    .UseNpgsql(connStr)
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

var app = builder.Build();

// ── Create table if not exists ─────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""MelxUnidades"" (
            ""Id""                  serial PRIMARY KEY,
            ""Fecha""               date NOT NULL,
            ""Horario""             text NOT NULL,
            ""HoraRegistro""        timestamp with time zone NOT NULL,
            ""ResponsableMkt""      text NOT NULL,
            ""Origen""              text NOT NULL,
            ""Destino""             text NOT NULL,
            ""LineaTransportista""  text NOT NULL,
            ""NombreOperador""      text NOT NULL,
            ""Placas""              text NOT NULL,
            ""NumeroCaja""          text NOT NULL,
            ""TelefonoOperador""    text NOT NULL,
            ""TipoMovimiento""      text NOT NULL DEFAULT 'Descarga',
            ""Estatus""             text NOT NULL DEFAULT 'Programada',
            ""PersonaAcceso""       text,
            ""HoraIngreso""         time,
            ""HoraSalida""          time,
            ""Comentario""          text,
            ""CreadoPor""           text NOT NULL,
            ""ActualizadoPor""      text,
            ""ActualizadoEn""       timestamp with time zone
        );
        CREATE INDEX IF NOT EXISTS ""IX_MelxUnidades_Fecha_Horario""
            ON ""MelxUnidades"" (""Fecha"", ""Horario"");
    ");
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

// ── Auth middleware ────────────────────────────────────────
string[] publicPaths = ["/login", "/api/login", "/health"];
app.Use(async (ctx, next) =>
{
    bool isPublic = publicPaths.Any(p => ctx.Request.Path.StartsWithSegments(p));
    var cookie = ctx.Request.Cookies["melx_session"];
    SessionUser? session = null;
    if (!string.IsNullOrEmpty(cookie))
        try { session = JsonSerializer.Deserialize<SessionUser>(cookie); } catch { }
    if (session is not null)
        ctx.RequestServices.GetRequiredService<CurrentUser>().Set(session);
    if (!isPublic && session is null) { ctx.Response.Redirect("/login"); return; }
    await next();
});

static void SetSession(HttpContext ctx, SessionUser user)
{
    ctx.Response.Cookies.Append("melx_session", JsonSerializer.Serialize(user), new CookieOptions
    {
        HttpOnly = true, SameSite = SameSiteMode.Lax,
        Secure = ctx.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    });
}

app.MapGet("/health", () => "Bitácora MELX OK");

// ── Login / Logout ─────────────────────────────────────────
app.MapPost("/api/login", async (HttpContext ctx, AuthService auth) =>
{
    var f = await ctx.Request.ReadFormAsync();
    var session = auth.Login(f["usuario"].ToString(), f["password"].ToString());
    if (session is null) return Results.Redirect("/login?error=1");
    SetSession(ctx, session);
    return Results.Redirect("/bitacora");
});

app.MapPost("/api/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("melx_session");
    return Results.Redirect("/login");
});

// ── POST /api/crear ────────────────────────────────────────
app.MapPost("/api/crear", async (HttpContext ctx, AppDbContext db, CurrentUser cu) =>
{
    if (!Permisos.PuedeRegistrar(cu.User!.Rol)) return Results.Forbid();
    var f = await ctx.Request.ReadFormAsync();

    var fecha   = DateOnly.Parse(f["fecha"].ToString());
    var horario = f["horario"].ToString().Trim();

    bool conflicto = await db.MelxUnidades
        .AnyAsync(u => u.Fecha == fecha && u.Horario == horario);

    db.MelxUnidades.Add(new BitacoraMelx.Models.MelxUnidad
    {
        Fecha              = fecha,
        Horario            = horario,
        HoraRegistro       = DateTime.UtcNow,
        ResponsableMkt     = f["responsableMkt"].ToString().Trim(),
        Origen             = f["origen"].ToString().Trim(),
        Destino            = f["destino"].ToString().Trim(),
        LineaTransportista = f["lineaTransportista"].ToString().Trim(),
        NombreOperador     = f["nombreOperador"].ToString().Trim(),
        Placas             = f["placas"].ToString().Trim(),
        NumeroCaja         = f["numeroCaja"].ToString().Trim(),
        TelefonoOperador   = f["telefonoOperador"].ToString().Trim(),
        TipoMovimiento     = f["tipoMovimiento"].ToString(),
        Estatus            = "Programada",
        CreadoPor          = cu.User.Usuario,
    });
    await db.SaveChangesAsync();

    var dest = $"/bitacora?fecha={fecha:yyyy-MM-dd}";
    if (conflicto) dest += "&warn=conflicto";
    return Results.Redirect(dest);
});

// ── POST /api/actualizar/{id} ──────────────────────────────
app.MapPost("/api/actualizar/{id:int}", async (int id, HttpContext ctx, AppDbContext db, CurrentUser cu) =>
{
    if (!Permisos.PuedeCompletarOperaciones(cu.User!.Rol)) return Results.Forbid();
    var f      = await ctx.Request.ReadFormAsync();
    var unidad = await db.MelxUnidades.FindAsync(id);
    if (unidad is null) return Results.NotFound();

    static string? nullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    unidad.Estatus       = f["estatus"].ToString();
    unidad.PersonaAcceso = nullIfEmpty(f["personaAcceso"].ToString());
    unidad.HoraIngreso   = TimeOnly.TryParse(f["horaIngreso"].ToString(), out var hi) ? hi : null;
    unidad.HoraSalida    = TimeOnly.TryParse(f["horaSalida"].ToString(), out var hs) ? hs : null;
    unidad.Comentario    = nullIfEmpty(f["comentario"].ToString());
    unidad.ActualizadoPor = cu.User.Usuario;
    unidad.ActualizadoEn  = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Redirect($"/bitacora?fecha={unidad.Fecha:yyyy-MM-dd}");
});

app.MapRazorComponents<BitacoraMelx.Components.App>().DisableAntiforgery();

app.Run();
```

- [ ] **Step 4: Verify build**

```bash
cd ~/Desktop/bitacora-melx
dotnet build
```

Expected: Build succeeded (will warn about missing Razor components — that's fine, they come in Task 3).

- [ ] **Step 5: Commit**

```bash
cd ~/Desktop/bitacora-melx
git add -A
git commit -m "feat: data model + DbContext + Program.cs with all endpoints"
```

---

### Task 3: Blazor shell + login page

**Files:**
- Create: `~/Desktop/bitacora-melx/Components/App.razor`
- Create: `~/Desktop/bitacora-melx/Components/Routes.razor`
- Create: `~/Desktop/bitacora-melx/Components/_Imports.razor`
- Create: `~/Desktop/bitacora-melx/Components/Shared/MainLayout.razor`
- Create: `~/Desktop/bitacora-melx/Components/Shared/NavMenu.razor`
- Create: `~/Desktop/bitacora-melx/Components/Pages/Login.razor`

**Interfaces:**
- Consumes: `CurrentUser` from Task 1; `Permisos`, `RolMelx` from Task 1
- Produces: Blazor routing shell, `/login` page, auth-gated layout

- [ ] **Step 1: Write Components/_Imports.razor**

```razor
@using BitacoraMelx.Components
@using BitacoraMelx.Components.Shared
@using BitacoraMelx.Models
@using BitacoraMelx.Security
@using BitacoraMelx.Data
@using Microsoft.EntityFrameworkCore
```

- [ ] **Step 2: Write Components/App.razor**

```razor
@namespace BitacoraMelx.Components

<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Bitácora MELX</title>
    <base href="/" />
    <link rel="stylesheet" href="css/app.css?v=1" />
    <HeadOutlet />
</head>
<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

- [ ] **Step 3: Write Components/Routes.razor**

```razor
@namespace BitacoraMelx.Components

<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

- [ ] **Step 4: Write Components/Shared/MainLayout.razor**

```razor
@namespace BitacoraMelx.Components.Shared
@inherits LayoutComponentBase

<div class="layout">
    <NavMenu />
    <main class="content">
        @Body
    </main>
</div>
```

- [ ] **Step 5: Write Components/Shared/NavMenu.razor**

```razor
@namespace BitacoraMelx.Components.Shared
@inject CurrentUser Current

<nav class="navbar">
    <span class="brand">Bitácora MELX</span>
    @if (Current.IsAuthenticated && Current.User is not null)
    {
        <a href="/bitacora" class="nav-link">Bitácora</a>
        <span class="nav-user">@Current.User.Usuario · @Current.User.Rol</span>
        <form method="post" action="/api/logout" style="display:inline;margin:0">
            <button type="submit" class="nav-logout" title="Salir">Salir</button>
        </form>
    }
    else
    {
        <a href="/login">Iniciar sesión</a>
    }
</nav>
```

- [ ] **Step 6: Write Components/Pages/Login.razor**

```razor
@page "/login"

<div class="login-hero">
    <img src="/melx-logo.png" style="height:56px;margin-bottom:1rem" alt="MELCO GIS" />
    <h1 style="font-size:1.6rem;margin-bottom:.5rem">Bitácora MELX</h1>
    <p style="color:var(--muted);margin-bottom:1.5rem">Control de Acceso de Unidades</p>

    @if (Error == "1")
    {
        <div class="alert-error">Usuario o contraseña incorrectos.</div>
    }

    <form method="post" action="/api/login" class="form-login" style="margin:0 auto">
        <label>Usuario</label>
        <input type="text" name="usuario" class="input" required autofocus placeholder="Tu nombre" />

        <label>Contraseña</label>
        <input type="password" name="password" class="input" required />

        <button type="submit" class="btn-primary">Entrar</button>
    </form>
</div>

@code {
    [SupplyParameterFromQuery] public string? Error { get; set; }
}
```

- [ ] **Step 7: Verify build**

```bash
cd ~/Desktop/bitacora-melx
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Smoke-test login locally**

Ensure you have a local Postgres running (or point `ConnectionStrings:Default` to the Railway Postgres). Then:

```bash
cd ~/Desktop/bitacora-melx
dotnet run
```

Navigate to `http://localhost:5000/login`. Verify:
- Login page renders with logo placeholder area.
- Wrong password → stays on `/login?error=1` with error message.
- Correct password (e.g. `mkt2026`) → redirects to `/bitacora` (404 for now — Bitacora page comes in Task 4).

- [ ] **Step 9: Commit**

```bash
cd ~/Desktop/bitacora-melx
git add -A
git commit -m "feat: blazor shell + login page"
```

---

### Task 4: Bitácora page + CSS + logo

**Files:**
- Create: `~/Desktop/bitacora-melx/Components/Pages/Bitacora.razor`
- Create: `~/Desktop/bitacora-melx/wwwroot/css/app.css`
- Create: `~/Desktop/bitacora-melx/wwwroot/melx-logo.png` (copy from source)

**Interfaces:**
- Consumes: `AppDbContext.MelxUnidades` from Task 2; `Permisos`, `CurrentUser`, `RolMelx` from Task 1
- Produces: `/bitacora` page — list + create flow; all role-based UI gating

- [ ] **Step 1: Copy the logo**

```bash
cp /private/tmp/bitacora_extract/Data/image1-23.png ~/Desktop/bitacora-melx/wwwroot/melx-logo.png
```

- [ ] **Step 2: Create wwwroot/css/app.css**

Copy the full `app.css` from `~/Desktop/JeDax/wwwroot/css/app.css` — it contains all needed variables, `.navbar`, `.btn-primary`, `.btn-outline`, `.btn-sm`, `.tabla`, `.form-section`, `.form-login`, `.alert-warn`, `.alert-error`, `.badge`, `.badge-programada`, `.badge-enplanta`, `.badge-salida`, `.badge-retenida`, `.empty-state`, and `.login-hero`. Then add at the end:

```css
/* Bitácora MELX — login hero */
.login-hero {
    max-width: 380px;
    margin: 8vh auto 0;
    text-align: center;
}
```

- [ ] **Step 3: Write Components/Pages/Bitacora.razor**

```razor
@page "/bitacora"
@inject AppDbContext Db
@inject CurrentUser Current

<div style="display:flex;align-items:center;gap:1rem;margin-bottom:1.5rem">
    <img src="/melx-logo.png" style="height:48px" alt="MELCO GIS" />
    <h1 style="margin:0;font-size:1.6rem">Control de Acceso de Unidades</h1>
</div>

@if (Warn == "conflicto")
{
    <div class="alert-warn" style="margin-bottom:1rem">
        ⚠ Ya existe otra unidad programada en el mismo horario. Se registró de todas formas.
    </div>
}

<form method="get" style="display:flex;gap:.75rem;align-items:center;flex-wrap:wrap;margin-bottom:1.5rem">
    <input type="date" name="fecha" value="@_fecha.ToString("yyyy-MM-dd")" class="input" style="width:auto" />
    <select name="estatus" class="input" style="width:auto">
        <option value="" selected="@string.IsNullOrEmpty(Estatus)">Todos los estatus</option>
        @foreach (var e in _estatusOpts)
        {
            <option value="@e" selected="@(Estatus == e)">@NombreEstatus(e)</option>
        }
    </select>
    <button type="submit" class="btn-outline">Filtrar</button>
    @if (Permisos.PuedeRegistrar(Current.User!.Rol))
    {
        <a href="#nueva" class="btn-primary" style="margin-left:auto;text-decoration:none">+ Nueva unidad</a>
    }
</form>

@if (_unidades.Count == 0)
{
    <div class="empty-state">
        <h3>Sin unidades</h3>
        <p>No hay unidades programadas para @_fecha.ToString("dd/MM/yyyy").</p>
    </div>
}
else
{
    <p style="color:var(--muted);font-size:12px;margin-bottom:.75rem">
        @_unidades.Count unidad(es) — @_fecha.ToString("dd/MM/yyyy")
    </p>
    <div style="overflow-x:auto">
        <table class="tabla">
            <thead>
                <tr>
                    <th>Horario</th>
                    <th>Hora Registro</th>
                    <th>Responsable</th>
                    <th>Origen</th>
                    <th>Destino</th>
                    <th>Transportista</th>
                    <th>Operador</th>
                    <th>Placas</th>
                    <th>Caja</th>
                    <th>Teléfono</th>
                    <th>Tipo</th>
                    <th>Estatus</th>
                    <th>Acceso</th>
                    <th>Ingreso</th>
                    <th>Salida</th>
                    <th>Comentario</th>
                    @if (Permisos.PuedeCompletarOperaciones(Current.User!.Rol))
                    {
                        <th></th>
                    }
                </tr>
            </thead>
            <tbody>
                @foreach (var u in _unidades)
                {
                    bool conflicto = _horariosConflicto.Contains(u.Horario);
                    bool editando  = Editar == u.Id;
                    <tr>
                        <td>
                            @if (conflicto) { <span title="Horario en conflicto">⚠ </span> }
                            @u.Horario
                        </td>
                        <td>@u.HoraRegistro.ToLocalTime().ToString("dd/MM/yy HH:mm")</td>
                        <td>@u.ResponsableMkt</td>
                        <td>@u.Origen</td>
                        <td>@u.Destino</td>
                        <td>@u.LineaTransportista</td>
                        <td>@u.NombreOperador</td>
                        <td>@u.Placas</td>
                        <td>@u.NumeroCaja</td>
                        <td>@u.TelefonoOperador</td>
                        <td>@u.TipoMovimiento</td>
                        <td>
                            <span class="badge badge-@u.Estatus.ToLower()">
                                @NombreEstatus(u.Estatus)
                            </span>
                        </td>
                        <td>@u.PersonaAcceso</td>
                        <td>@u.HoraIngreso?.ToString("HH:mm")</td>
                        <td>@u.HoraSalida?.ToString("HH:mm")</td>
                        <td>@u.Comentario</td>
                        @if (Permisos.PuedeCompletarOperaciones(Current.User!.Rol))
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
                            <td colspan="17" style="padding:1rem">
                                <form method="post" action="/api/actualizar/@u.Id"
                                      style="display:flex;gap:.75rem;flex-wrap:wrap;align-items:flex-end">
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Estatus</label>
                                        <select name="estatus" class="input" style="width:auto">
                                            @foreach (var e in _estatusOpts)
                                            {
                                                <option value="@e" selected="@(u.Estatus == e)">
                                                    @NombreEstatus(e)
                                                </option>
                                            }
                                        </select>
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Persona que da acceso</label>
                                        <input name="personaAcceso" value="@u.PersonaAcceso"
                                               class="input" placeholder="Nombre" />
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Hora ingreso</label>
                                        <input type="time" name="horaIngreso"
                                               value="@u.HoraIngreso?.ToString("HH:mm")"
                                               class="input" style="width:auto" />
                                    </div>
                                    <div>
                                        <label style="font-size:11px;color:var(--muted)">Hora salida</label>
                                        <input type="time" name="horaSalida"
                                               value="@u.HoraSalida?.ToString("HH:mm")"
                                               class="input" style="width:auto" />
                                    </div>
                                    <div style="flex:1;min-width:180px">
                                        <label style="font-size:11px;color:var(--muted)">Comentario</label>
                                        <input name="comentario" value="@u.Comentario"
                                               class="input" placeholder="Observaciones" />
                                    </div>
                                    <button type="submit" class="btn-primary">✓ Guardar</button>
                                    <a href="/bitacora?fecha=@_fecha.ToString("yyyy-MM-dd")"
                                       class="btn-outline">Cancelar</a>
                                </form>
                            </td>
                        </tr>
                    }
                }
            </tbody>
        </table>
    </div>
}

@if (Permisos.PuedeRegistrar(Current.User!.Rol))
{
    <section id="nueva" style="margin-top:2.5rem;max-width:480px">
        <h2 style="font-size:1rem;margin-bottom:1rem">+ Registrar nueva unidad</h2>
        <form method="post" action="/api/crear" class="form-section">
            <input type="hidden" name="fecha" value="@_fecha.ToString("yyyy-MM-dd")" />

            <label>Horario programado</label>
            <input name="horario" required class="input" placeholder="ej: 08:00–10:00" />

            <label>Responsable</label>
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
            <input type="tel" name="telefonoOperador" required class="input" />

            <label>Tipo de movimiento</label>
            <select name="tipoMovimiento" required class="input">
                <option value="Descarga">Descarga</option>
                <option value="Carga">Carga</option>
            </select>

            <button type="submit" class="btn-primary">✓ Registrar unidad</button>
        </form>
    </section>
}

@code {
    [SupplyParameterFromQuery] public string? Fecha   { get; set; }
    [SupplyParameterFromQuery] public string? Estatus { get; set; }
    [SupplyParameterFromQuery] public int?    Editar  { get; set; }
    [SupplyParameterFromQuery] public string? Warn    { get; set; }

    DateOnly _fecha = DateOnly.FromDateTime(DateTime.Today);
    List<MelxUnidad> _unidades = [];
    HashSet<string> _horariosConflicto = [];

    static readonly string[] _estatusOpts =
        ["Programada", "EnPlanta", "Salida", "Retenida"];

    protected override async Task OnInitializedAsync()
    {
        if (DateOnly.TryParse(Fecha, out var f)) _fecha = f;

        var q = Db.MelxUnidades.Where(u => u.Fecha == _fecha);
        if (!string.IsNullOrEmpty(Estatus))
            q = q.Where(u => u.Estatus == Estatus);

        _unidades = await q.OrderBy(u => u.Horario).ThenBy(u => u.HoraRegistro).ToListAsync();

        _horariosConflicto = _unidades
            .GroupBy(u => u.Horario)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();
    }

    static string NombreEstatus(string e) => e switch
    {
        "Programada" => "Programada",
        "EnPlanta"   => "En planta",
        "Salida"     => "Salida",
        "Retenida"   => "Retenida",
        _            => e
    };
}
```

- [ ] **Step 4: Verify build**

```bash
cd ~/Desktop/bitacora-melx
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: End-to-end test**

```bash
dotnet run
```

Navigate to `http://localhost:5000`. Log in with `mkt2026`. Verify:
- `/bitacora` renders with MELCO.GIS logo and title "Control de Acceso de Unidades".
- "Nueva unidad" form is visible (MKT role can register).
- No "Actualizar" buttons (MKT cannot complete Operaciones).
- Submit the form → row appears in table with HoraRegistro visible.
- Submit a second row with same Horario → `?warn=conflicto` banner appears, both rows show ⚠.
- Log out, log in with `ope2026` → no registration form, "Actualizar" buttons visible.
- Click "Actualizar" → inline form expands with Estatus, PersonaAcceso, HoraIngreso, HoraSalida, Comentario fields.
- Submit update → status badge changes.

- [ ] **Step 6: Commit**

```bash
cd ~/Desktop/bitacora-melx
git add -A
git commit -m "feat: bitacora page with create, update, conflict detection, badges"
```

---

### Task 5: JeDax cleanup — remove Bitácora module

**Working directory: `~/Desktop/JeDax`**

This task removes everything we added to JeDax for the Bitácora module (since it now lives in bitacora-melx). After each file edit, `dotnet build` must pass before committing.

**Files:**
- Delete: `Components/Pages/Bitacora.razor`
- Delete: `Models/UnidadAcceso.cs`
- Delete: `Helpers/StringExtensions.cs`
- Delete: `Migrations/20260819155146_AddUnidadAcceso.cs`
- Delete: `Migrations/20260819155146_AddUnidadAcceso.Designer.cs`
- Modify: `Migrations/AppDbContextModelSnapshot.cs` — remove UnidadAcceso entity block
- Modify: `Data/AppDbContext.cs` — remove DbSet and query filter
- Modify: `Security/Permisos.cs` — remove 3 Bitácora permission methods
- Modify: `Components/Shared/NavMenu.razor` — remove Bitácora nav link
- Modify: `Program.cs` — remove CREATE TABLE block + 2 endpoints
- Delete: `wwwroot/melx-logo.png`
- Modify: `wwwroot/css/app.css` — remove 4 badge lines

**Interfaces:**
- Consumes: nothing (cleanup only)
- Produces: clean JeDax build without any Bitácora reference

- [ ] **Step 1: Delete Bitácora-specific files**

```bash
cd ~/Desktop/JeDax
rm Components/Pages/Bitacora.razor
rm Models/UnidadAcceso.cs
rm Helpers/StringExtensions.cs
rm Migrations/20260819155146_AddUnidadAcceso.cs
rm Migrations/20260819155146_AddUnidadAcceso.Designer.cs
rm wwwroot/melx-logo.png
```

- [ ] **Step 2: Remove DbSet and query filter from Data/AppDbContext.cs**

In `Data/AppDbContext.cs`, remove line 25:
```csharp
public DbSet<UnidadAcceso> UnidadAccesos => Set<UnidadAcceso>();
```

And remove the entire UnidadAcceso entity block (lines 84-92, approximately):
```csharp
        // ── UnidadAcceso ──────────────────────────────────────────
        mb.Entity<UnidadAcceso>(e =>
        {
            e.HasQueryFilter(u => u.TenantId == _tenant.TenantId);
            e.HasIndex(u => new { u.TenantId, u.Fecha });
        });
```

- [ ] **Step 3: Remove 3 Bitácora methods from Security/Permisos.cs**

Remove lines 49-55 from `Security/Permisos.cs`:
```csharp
    public static bool PuedeCrearBitacora(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mkt;
    public static bool PuedeCompletarBitacora(RolUsuario rol) =>
        rol is RolUsuario.Admin or RolUsuario.Mlx;
    public static bool PuedeVerBitacora(RolUsuario _) => true;
```

- [ ] **Step 4: Remove Bitácora nav link from Components/Shared/NavMenu.razor**

Remove lines 29-32:
```razor
        @if (Permisos.PuedeVerBitacora(Current.User.Rol))
        {
            <a href="/bitacora" class="nav-link"><Icon Name="truck" Size="16" /><span>Bitácora</span></a>
        }
```

- [ ] **Step 5: Remove CREATE TABLE block and 2 endpoints from Program.cs**

In `Program.cs`, remove the `CREATE TABLE IF NOT EXISTS "UnidadAccesos"` block (lines ~75-102) inside the startup `using (var scope ...)` block.

Remove `app.MapPost("/api/bitacora/crear", ...)` — the full handler (lines ~375-412).

Remove `app.MapPost("/api/bitacora/actualizar/{id:int}", ...)` — the full handler (lines ~413-435).

- [ ] **Step 6: Remove 4 badge lines from wwwroot/css/app.css**

Remove lines 397-400:
```css
.badge-programada { background: rgba(255,255,255,.08); color: var(--muted); border-color: rgba(255,255,255,.15); }
.badge-enplanta   { background: rgba(46,204,113,.12); color: var(--green); border-color: rgba(46,204,113,.3); }
.badge-salida     { background: rgba(77,158,255,.12); color: var(--blue);  border-color: rgba(77,158,255,.3); }
.badge-retenida   { background: rgba(255,77,77,.12);  color: var(--red);   border-color: rgba(255,77,77,.3); }
```

- [ ] **Step 7: Update Migrations/AppDbContextModelSnapshot.cs**

Open `Migrations/AppDbContextModelSnapshot.cs`. Find and remove the entire `modelBuilder.Entity("JeDax.Models.UnidadAcceso", ...)` block — it starts with that line and ends with its closing `});`. This block is typically 60-80 lines.

- [ ] **Step 8: Verify JeDax builds cleanly**

```bash
cd ~/Desktop/JeDax
dotnet build
```

Expected: Build succeeded, 0 errors, 0 warnings about UnidadAcceso.

- [ ] **Step 9: Commit JeDax cleanup**

```bash
cd ~/Desktop/JeDax
git add -A
git commit -m "chore: remove bitacora module (moved to standalone bitacora-melx app)"
git push origin main
```

- [ ] **Step 10: Commit bitacora-melx initial version**

```bash
cd ~/Desktop/bitacora-melx
git add -A
git commit -m "feat: complete bitacora-melx standalone app"
```

---

## Self-Review

**Spec coverage check:**
- ✅ New .NET 10 app at `~/Desktop/bitacora-melx`
- ✅ PostgreSQL shared instance, table `MelxUnidades`
- ✅ kitting-web-style auth: one password per role via env vars
- ✅ Roles: ADM, MKT, OPE, MELX with correct permission matrix
- ✅ `HoraRegistro` visible in table (Task 4)
- ✅ Conflict detection + ⚠ indicator + warning banner
- ✅ Status badges: Programada, EnPlanta, Salida, Retenida
- ✅ MELCO.GIS logo in header
- ✅ MKT/MELX/ADM see registration form; OPE/ADM see Actualizar buttons
- ✅ JeDax cleanup (Task 5)

**Placeholder scan:** No TBDs. All code blocks are complete.

**Type consistency:** `MelxUnidad` POCO used throughout; `RolMelx` enum used in all permission checks; `AppDbContext.MelxUnidades` DbSet matches `ToTable("MelxUnidades")` and CREATE TABLE statement.
