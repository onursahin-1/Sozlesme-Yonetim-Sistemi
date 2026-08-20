using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly string _connectionString;

    public AuditLogRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(AuditLog log)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
