using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class PasswordResetRequestRepository : IPasswordResetRequestRepository
{
    private readonly string _connectionString;

    public PasswordResetRequestRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(PasswordResetRequest request)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.PasswordResetRequests.Add(request);
        await db.SaveChangesAsync();
    }

    public async Task<List<PasswordResetRequest>> GetPendingAsync()
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.PasswordResetRequests
            .AsNoTracking()
            .Where(r => !r.IsHandled)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();
    }

    public async Task MarkHandledForUserAsync(int userId, int handledByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var pending = await db.PasswordResetRequests
            .Where(r => r.UserId == userId && !r.IsHandled)
            .ToListAsync();
        if (pending.Count == 0) return;

        var now = DateTime.Now;
        foreach (var request in pending)
        {
            request.IsHandled = true;
            request.HandledAt = now;
            request.HandledByUserId = handledByUserId;
        }

        await db.SaveChangesAsync();
    }

    public async Task MarkHandledAsync(int requestId, int handledByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var request = await db.PasswordResetRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null || request.IsHandled) return;

        request.IsHandled = true;
        request.HandledAt = DateTime.Now;
        request.HandledByUserId = handledByUserId;
        await db.SaveChangesAsync();
    }

    public async Task<DateTime?> GetLastRequestAtAsync(string username)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.PasswordResetRequests
            .AsNoTracking()
            .Where(r => r.Username == username)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => (DateTime?)r.RequestedAt)
            .FirstOrDefaultAsync();
    }
}
