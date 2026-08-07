using JeDax.Data;
using JeDax.Security;
using JeDax.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

var app = builder.Build();

// Database setup:
// - SQLite (dev): apply migrations.
// - PostgreSQL (prod): use EnsureCreated — genera schema desde el modelo con tipos correctos,
//   evitando problemas de migraciones generadas con SQLite. Resetea si el schema está corrupto.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (usePostgres)
    {
        // Detectar schema corrupto (columnas con tipos de SQLite) y resetear
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        bool needsReset = false;
        bool tablesExist = false;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT data_type FROM information_schema.columns
                                WHERE table_name = 'Tenants' AND column_name = 'Activo'";
            var result = await cmd.ExecuteScalarAsync();
            tablesExist = result != null;
            if (tablesExist && result?.ToString() != "boolean")
                needsReset = true;
        }
        await conn.CloseAsync();

        if (needsReset)
            await db.Database.EnsureDeletedAsync();

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

app.UseWebSockets();
app.UseStaticFiles();

app.MapGet("/health", () => "JeDax OK");
app.MapGet("/", () => Results.Redirect("/login"));

// Fallback login por HTTP POST (no requiere circuito Blazor).
app.MapPost("/api/login", async (HttpContext ctx, AuthService auth) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var slug = form["slug"].ToString().Trim();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString().Trim();

    var (ok, session) = await auth.LoginAsync(slug, username, password);
    if (!ok || session is null)
        return Results.Redirect("/login?error=1");

    var sessionJson = System.Text.Json.JsonSerializer.Serialize(session);
    var escaped = System.Text.Json.JsonSerializer.Serialize(sessionJson);
    var html = $@"<!DOCTYPE html><html><head><meta charset=""utf-8""><title>JeDax</title></head><body>
<script>
localStorage.setItem('jedax_session', {escaped});
window.location.href = '/stock';
</script>
<p>Iniciando sesión…</p>
</body></html>";
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapRazorComponents<JeDax.Components.App>()
    .AddInteractiveServerRenderMode()
    .DisableAntiforgery();

app.Run();
