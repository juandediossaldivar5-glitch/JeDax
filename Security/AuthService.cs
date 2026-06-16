using JeDax.Data;
using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Security;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(bool ok, SessionUser? session)> LoginAsync(string slug, string username, string password)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.Activo);

        if (tenant is null)
            return (false, null);

        var user = await _db.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenant.Id
                                   && u.Username == username
                                   && u.Activo);

        if (user is null)
            return (false, null);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, null);

        return (true, new SessionUser
        {
            Id = user.Id,
            TenantId = tenant.Id,
            TenantSlug = tenant.Slug,
            Username = user.Username,
            Rol = user.Rol,
            IsAuthenticated = true
        });
    }
}

