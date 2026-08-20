using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class ScheduledJobRepository : IScheduledJobRepository
{
    private readonly string _connectionString;

    public ScheduledJobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task EnsureJobExistsAsync(string jobName)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var exists = await db.ScheduledJobRuns.AnyAsync(j => j.JobName == jobName);
        if (exists) return;

        db.ScheduledJobRuns.Add(new ScheduledJobRun { JobName = jobName });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Başka bir istemci aynı anda oluşturmuş olabilir; JobName üzerindeki unique
            // index bunu engeller. Satır zaten var olduğu için yapılacak bir şey yok.
        }
    }

    public async Task<bool> TryAcquireAsync(string jobName, TimeSpan interval, TimeSpan lease, string owner)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var now = DateTime.Now;
        var dueBefore = now - interval;
        var lockUntil = now + lease;

        // Kontrol ve kilitleme tek bir UPDATE içinde yapılıyor. "Önce oku, sonra yaz"
        // yaklaşımı iki istemcinin arayı kaçırıp ikisinin birden işi üstlenmesine izin
        // verirdi; burada koşullar WHERE'de olduğu için yarış durumu oluşmaz.
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ScheduledJobRuns
            SET LockedUntil = {lockUntil}, LockedBy = {owner}
            WHERE JobName = {jobName}
              AND (LastRunAt IS NULL OR LastRunAt <= {dueBefore})
              AND (LockedUntil IS NULL OR LockedUntil <= {now})");

        return affected == 1;
    }

    public async Task ReleaseAsync(string jobName, string? result)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var now = DateTime.Now;
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE ScheduledJobRuns
            SET LastRunAt = {now}, LockedUntil = NULL, LockedBy = NULL, LastResult = {result}
            WHERE JobName = {jobName}");
    }
}
