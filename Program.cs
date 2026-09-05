using JeDax.Data;
using JeDax.Models;
using JeDax.Security;
using JeDax.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();

bool usePostgres = builder.Configuration.GetValue<bool>("UsePostgres");

if (usePostgres)
{
    var cs = string.Format("Host={0};Port={1};Database={2};Username={3};Password={4}",
        Environment.GetEnvironmentVariable("PGHOST"),
        Environment.GetEnvironmentVariable("PGPORT"),
        Environment.GetEnvironmentVariable("PGDATABASE"),
        Environment.GetEnvironmentVariable("PGUSER"),
        Environment.GetEnvironmentVariable("PGPASSWORD"));
    builder.Services.AddDbContext<AppDbContext>(opt => opt
        .UseNpgsql(cs)
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite(connectionString ?? "Data Source=jedax_dev.db"));
}

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<ValeService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CaseGeneradorService>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<ValeImportService>();
builder.Services.AddScoped<SakumaImportService>();
builder.Services.AddScoped<CurrentUser>();

var app = builder.Build();

// DB setup con detección de schema corrupto
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        bool needsReset = false, tablesExist = false;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT data_type FROM information_schema.columns
                                WHERE table_name = 'Tenants' AND column_name = 'Activo'";
            var result = await cmd.ExecuteScalarAsync();
            tablesExist = result != null;
            if (tablesExist && result?.ToString() != "boolean") needsReset = true;
        }
        await conn.CloseAsync();

        if (needsReset) await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }

    await JeDax.Data.DbSeeder.SeedAsync(db);
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

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();

// ── Middleware de auth por cookie ──────────────────────────────
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    bool isPublic = path == "/" || path == "/login" || path == "/health"
                    || path.StartsWith("/api/login")
                    || path.StartsWith("/css")
                    || path.StartsWith("/_framework")
                    || path.StartsWith("/js");

    var cookie = ctx.Request.Cookies["jedax_session"];
    SessionUser? session = null;
    if (!string.IsNullOrEmpty(cookie))
    {
        try { session = JsonSerializer.Deserialize<SessionUser>(cookie); } catch { }
    }

    if (session is not null)
    {
        var current = ctx.RequestServices.GetRequiredService<CurrentUser>();
        current.Set(session);
        var tenant = ctx.RequestServices.GetRequiredService<TenantContext>();
        tenant.Set(session.TenantId, session.TenantSlug);
    }

    if (!isPublic && session is null)
    {
        ctx.Response.Redirect("/login");
        return;
    }

    await next();
});

// ── Helpers ────────────────────────────────────────────────────
static string Back(HttpContext ctx, string fallback = "/stock")
{
    var referer = ctx.Request.Headers.Referer.ToString();
    return string.IsNullOrEmpty(referer) ? fallback : referer;
}

static void SetSession(HttpContext ctx, SessionUser user)
{
    var json = JsonSerializer.Serialize(user);
    ctx.Response.Cookies.Append("jedax_session", json, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = ctx.Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    });
}

// ── Endpoints públicos ──────────────────────────────────────────
app.MapGet("/health", () => "JeDax OK");
app.MapGet("/", (HttpContext ctx) =>
{
    var cookie = ctx.Request.Cookies["jedax_session"];
    return string.IsNullOrEmpty(cookie) ? Results.Redirect("/login") : Results.Redirect("/stock");
});

app.MapPost("/api/login", async (HttpContext ctx, AuthService auth) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var slug = form["slug"].ToString().Trim();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString().Trim();

    var (ok, session) = await auth.LoginAsync(slug, username, password);
    if (!ok || session is null) return Results.Redirect("/login?error=1");

    SetSession(ctx, session);
    return Results.Redirect("/stock");
});

app.MapPost("/api/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("jedax_session");
    return Results.Redirect("/login");
});

// ── Inventario ────────────────────────────────────────────────
app.MapPost("/api/inventario/entrada", async (HttpContext ctx, InventarioService inv, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        await inv.RegistrarEntradaAsync(
            f["caseCode"].ToString(), f["item"].ToString(), f["descripcion"].ToString(),
            int.Parse(f["qty"].ToString()), f["refEntrada"].ToString(),
            cu.User!.Username, cu.User.TenantId);
        return Results.Redirect(Back(ctx, "/ingreso"));
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/ingreso?error={Uri.EscapeDataString(ex.Message)}");
    }
});

app.MapPost("/api/inventario/salida", async (HttpContext ctx, InventarioService inv, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        int qty = int.TryParse(f["qty"].ToString(), out var q) ? q : 0;
        await inv.RegistrarSalidaAsync(f["caseCode"].ToString(), f["refSalida"].ToString(), qty, cu.User!.Username);
        return Results.Redirect(Back(ctx, "/salidas"));
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/salidas?error={Uri.EscapeDataString(ex.Message)}");
    }
});

app.MapPost("/api/inventario/mover", async (HttpContext ctx, InventarioService inv, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        await inv.MoverCaseAsync(f["caseCode"].ToString(), f["ubicacion"].ToString(), cu.User!.Username);
        return Results.Redirect($"/move?ok=1&code={Uri.EscapeDataString(f["caseCode"].ToString())}");
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/move?error={Uri.EscapeDataString(ex.Message)}&code={Uri.EscapeDataString(f["caseCode"].ToString())}");
    }
});

// ── Vales (workflow ingreso/salidas) ───────────────────────────
app.MapPost("/api/vales/marcar-procesado", async (HttpContext ctx, ValeService vs) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        int valeId = int.Parse(f["valeId"].ToString());
        await vs.MarcarDetalleProcesadoAsync(valeId, f["caseCode"].ToString(), ctx.RequestServices.GetRequiredService<CurrentUser>().User!.Username);
        await vs.CerrarSiCompletoAsync(valeId);
        return Results.Redirect(Back(ctx, "/vales"));
    }
    catch (Exception ex)
    {
        return Results.Redirect($"{Back(ctx, "/vales")}&error={Uri.EscapeDataString(ex.Message)}");
    }
});

app.MapPost("/api/vales/cancelar", async (HttpContext ctx, ValeService vs) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        await vs.CancelarValeAsync(int.Parse(f["valeId"].ToString()));
    }
    catch { }
    return Results.Redirect("/vales");
});

app.MapPost("/api/vales/importar", async (HttpContext ctx, ValeImportService imp, CurrentUser cu) =>
{
    var f = await ctx.Request.ReadFormAsync();
    var file = f.Files.GetFile("archivo");
    if (file is null) return Results.Redirect("/vales?error=Sin+archivo");
    try
    {
        using var s = file.OpenReadStream();
        var preview = await imp.ParsearAsync(s);
        await imp.CommitAsync(preview, cu.User!.Username);
        return Results.Redirect("/vales?ok=1");
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/vales?error={Uri.EscapeDataString(ex.Message)}");
    }
});

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
        code, items[i] ?? "", descripciones[i] ?? "", int.Parse(qtys[i] ?? "0"))).ToList();

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

app.MapGet("/api/sakuma/cancelar", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("sakuma_preview");
    return Results.Redirect("/sakuma-import");
});

// ── Admin ──────────────────────────────────────────────────────
app.MapPost("/api/admin/crear-usuario", async (HttpContext ctx, UsuarioService us) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        Enum.TryParse<RolUsuario>(f["rol"].ToString(), out var rol);
        await us.CrearAsync(f["username"].ToString(), f["password"].ToString(), rol);
        return Results.Redirect("/admin?ok=1");
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/admin?error={Uri.EscapeDataString(ex.Message)}");
    }
});

app.MapPost("/api/admin/cambiar-password", async (HttpContext ctx, UsuarioService us) =>
{
    var f = await ctx.Request.ReadFormAsync();
    try
    {
        await us.CambiarPasswordAsync(int.Parse(f["userId"].ToString()), f["password"].ToString());
        return Results.Redirect("/admin?ok=1");
    }
    catch (Exception ex)
    {
        return Results.Redirect($"/admin?error={Uri.EscapeDataString(ex.Message)}");
    }
});

app.MapPost("/api/admin/cambiar-rol", async (HttpContext ctx, UsuarioService us) =>
{
    var f = await ctx.Request.ReadFormAsync();
    Enum.TryParse<RolUsuario>(f["rol"].ToString(), out var rol);
    await us.CambiarRolAsync(int.Parse(f["userId"].ToString()), rol);
    return Results.Redirect("/admin");
});

app.MapPost("/api/admin/toggle-activo", async (HttpContext ctx, UsuarioService us) =>
{
    var f = await ctx.Request.ReadFormAsync();
    await us.ToggleActivoAsync(int.Parse(f["userId"].ToString()));
    return Results.Redirect("/admin");
});

// ── Case Generador ────────────────────────────────────────────
app.MapGet("/api/case-generador/download", (int desde, int cantidad) =>
{
    if (cantidad <= 0 || cantidad > 5000) return Results.BadRequest();
    var codigos = CaseGeneradorService.GenerarCodigos(desde, cantidad);
    var texto = CaseGeneradorService.GenerarTextoEtiquetas(codigos);
    return Results.File(System.Text.Encoding.UTF8.GetBytes(texto), "text/plain", $"case-{desde}-{cantidad}.txt");
});

// ── Export ────────────────────────────────────────────────────
app.MapGet("/api/export/stock", async (ExportService ex) =>
{
    var bytes = await ex.ExportarStockAsync();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jedax_stock.xlsx");
});

app.MapGet("/api/export/historial", async (ExportService ex) =>
{
    var bytes = await ex.ExportarHistorialAsync();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jedax_historial.xlsx");
});

app.MapGet("/api/export/vales", async (ExportService ex) =>
{
    var bytes = await ex.ExportarValesAsync();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "jedax_vales.xlsx");
});

app.MapRazorComponents<JeDax.Components.App>().DisableAntiforgery();

app.Run();
