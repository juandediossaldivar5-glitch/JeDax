using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Services;

public class UsuarioService
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public UsuarioService(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<Usuario>> ListarAsync()
        => await _db.Usuarios.OrderBy(u => u.Username).ToListAsync();

    public async Task<Usuario> CrearAsync(string username, string password, RolUsuario rol)
    {
        username = username.Trim().ToUpperInvariant();

        if (await _db.Usuarios.AnyAsync(u => u.Username == username))
            throw new InvalidOperationException($"El usuario {username} ya existe.");

        var u = new Usuario
        {
            TenantId = _tenant.TenantId,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Rol = rol,
            Activo = true
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    public async Task CambiarPasswordAsync(int userId, string nuevaPassword)
    {
        var u = await _db.Usuarios.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(nuevaPassword);
        await _db.SaveChangesAsync();
    }

    public async Task ToggleActivoAsync(int userId)
    {
        var u = await _db.Usuarios.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");
        u.Activo = !u.Activo;
        await _db.SaveChangesAsync();
    }

    public async Task CambiarRolAsync(int userId, RolUsuario rol)
    {
        var u = await _db.Usuarios.FindAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");
        u.Rol = rol;
        await _db.SaveChangesAsync();
    }
}
