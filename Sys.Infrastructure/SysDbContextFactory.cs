using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sys.Infrastructure;

public class SysDbContextFactory : IDesignTimeDbContextFactory<SysDbContext>
{
    public SysDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SysDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=SysDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True");
        return new SysDbContext(optionsBuilder.Options);
    }
}