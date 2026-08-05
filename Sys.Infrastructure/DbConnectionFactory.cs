using Microsoft.EntityFrameworkCore;

namespace Sys.Infrastructure;

public static class DbConnectionFactory
{
    public static SysDbContext CreateContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SysDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new SysDbContext(optionsBuilder.Options);
    }
}