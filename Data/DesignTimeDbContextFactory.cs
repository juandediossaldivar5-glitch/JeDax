using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JeDax.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=jedax_dev.db")
            .Options;

        // TenantContext vacío — solo para migraciones
        var tenant = new TenantContext();

        return new AppDbContext(options, tenant);
    }
}
