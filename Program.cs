using JeDax.Data;
using JeDax.Security;
using JeDax.Services;
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

// Always run migrations. Seed only in Development.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
        await JeDax.Data.DbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/", () => Results.Redirect("/login"));

app.MapRazorComponents<JeDax.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
