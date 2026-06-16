// Data/DbSeeder.cs
// Ejecutar una sola vez con: dotnet run --seed
// O llamar desde Program.cs en desarrollo para garantizar datos base.

using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Tenant inicial (Laredo)
        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            var tenant = new Tenant
            {
                Nombre = "JeDax Laredo",
                Slug = "laredo",
                Activo = true
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            // Usuario admin inicial
            db.Usuarios.Add(new Usuario
            {
                TenantId = tenant.Id,
                Username = "JUAN_ADM",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("JD180794"),
                Rol = RolUsuario.Admin,
                Activo = true
            });

            // Usuarios operativos del sistema Laredo
            var operativos = new[]
            {
                ("OPE_LNK",    "LANDESK.2026", RolUsuario.Lnk),
                ("OPE_MKT",    "MKT2026",      RolUsuario.Mkt),
                ("OPE_EPQ",    "EPQ2026",      RolUsuario.Epq),
                ("OPE_LAREDO", "LAREDO2026",   RolUsuario.Laredo),
                ("OPE_MLX",    "Mexico.2026",  RolUsuario.Mlx),
            };

            foreach (var (user, pass, rol) in operativos)
            {
                db.Usuarios.Add(new Usuario
                {
                    TenantId = tenant.Id,
                    Username = user,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(pass),
                    Rol = rol,
                    Activo = true
                });
            }

            await db.SaveChangesAsync();
        }
    }
}
