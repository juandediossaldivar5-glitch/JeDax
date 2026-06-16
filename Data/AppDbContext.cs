using JeDax.Models;
using Microsoft.EntityFrameworkCore;

namespace JeDax.Data;

public class AppDbContext : DbContext
{
    private readonly TenantContext _tenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, TenantContext? tenant = null)
        : base(options)
    {
        _tenant = tenant ?? new TenantContext();
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Vale> Vales => Set<Vale>();
    public DbSet<ValeDetalle> ValeDetalles => Set<ValeDetalle>();
    public DbSet<MovimientoHistorico> Movimientos => Set<MovimientoHistorico>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── Tenant ──────────────────────────────────────────────
        mb.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // ── Usuario ──────────────────────────────────────────────
        mb.Entity<Usuario>(e =>
        {
            e.HasQueryFilter(u => u.TenantId == _tenant.TenantId);
            e.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
        });

        // ── Producto ─────────────────────────────────────────────
        mb.Entity<Producto>(e =>
        {
            e.HasQueryFilter(p => p.TenantId == _tenant.TenantId);
            e.HasIndex(p => new { p.TenantId, p.Item }).IsUnique();
        });

        // ── Case ─────────────────────────────────────────────────
        mb.Entity<Case>(e =>
        {
            e.HasQueryFilter(c => c.TenantId == _tenant.TenantId);
            e.HasIndex(c => new { c.TenantId, c.CaseCode }).IsUnique();
            e.Ignore(c => c.QtySalidas); // computed property
        });

        // ── Vale ─────────────────────────────────────────────────
        mb.Entity<Vale>(e =>
        {
            e.HasQueryFilter(v => v.TenantId == _tenant.TenantId);
            e.HasIndex(v => new { v.TenantId, v.Referencia }).IsUnique();
        });

        // ── ValeDetalle ──────────────────────────────────────────
        mb.Entity<ValeDetalle>(e =>
        {
            e.HasOne(d => d.Vale)
             .WithMany(v => v.Detalles)
             .HasForeignKey(d => d.ValeId)
             .OnDelete(DeleteBehavior.Cascade);
            // Mark navigation as optional so EF Core doesn't warn about
            // the required-end query filter mismatch with Vale's tenant filter.
            e.Navigation(d => d.Vale).IsRequired(false);
        });

        // ── MovimientoHistorico ───────────────────────────────────
        mb.Entity<MovimientoHistorico>(e =>
        {
            e.HasQueryFilter(m => m.TenantId == _tenant.TenantId);
        });
    }
}
