using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// AuthService ve NotificationService testleri için sahte depolar.
// Hepsi yazılanı bellekte tutuyor; testler "ne yazıldığına" bakıyor.

public class FakePasswordResetRequestRepository : IPasswordResetRequestRepository
{
    public List<PasswordResetRequest> Requests { get; } = new();

    // Hız sınırı testleri için: servis bu değeri okuyup 10 dakikalık pencereye bakıyor.
    public DateTime? LastRequestAt { get; set; }

    public Task AddAsync(PasswordResetRequest request)
    {
        request.Id = Requests.Count + 1;
        Requests.Add(request);
        return Task.CompletedTask;
    }

    public Task<List<PasswordResetRequest>> GetPendingAsync()
        => Task.FromResult(Requests.Where(r => !r.IsHandled).ToList());

    public Task MarkHandledForUserAsync(int userId, int handledByUserId)
    {
        foreach (var r in Requests.Where(r => r.UserId == userId && !r.IsHandled))
        {
            r.IsHandled = true;
            r.HandledAt = DateTime.Now;
            r.HandledByUserId = handledByUserId;
        }
        return Task.CompletedTask;
    }

    public Task MarkHandledAsync(int requestId, int handledByUserId)
    {
        var r = Requests.FirstOrDefault(x => x.Id == requestId);
        if (r is not null)
        {
            r.IsHandled = true;
            r.HandledAt = DateTime.Now;
            r.HandledByUserId = handledByUserId;
        }
        return Task.CompletedTask;
    }

    public Task<DateTime?> GetLastRequestAtAsync(string username) => Task.FromResult(LastRequestAt);
}

public class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Added { get; } = new();

    public Task<List<Notification>> GetForUserAsync(int userId, int take)
        => Task.FromResult(Added.Where(n => n.UserId == userId).Take(take).ToList());

    public Task<int> GetUnreadCountAsync(int userId)
        => Task.FromResult(Added.Count(n => n.UserId == userId && !n.IsRead));

    public Task MarkReadAsync(int notificationId, int userId) => Task.CompletedTask;
    public Task MarkAllReadAsync(int userId) => Task.CompletedTask;

    // Gerçek depo, DedupeKey'i dolu olup zaten var olan bildirimleri atlıyor.
    // Testlerde tekrar engelinin çalıştığını görebilmek için aynı davranış burada da var.
    public Task<int> AddManyAsync(List<Notification> notifications)
    {
        var inserted = 0;
        foreach (var n in notifications)
        {
            if (n.DedupeKey is not null &&
                Added.Any(e => e.UserId == n.UserId && e.DedupeKey == n.DedupeKey))
                continue;

            Added.Add(n);
            inserted++;
        }
        return Task.FromResult(inserted);
    }

    public Task DeleteReadAsync(int notificationId, int userId) => Task.CompletedTask;
    public Task<int> DeleteAllReadAsync(int userId) => Task.FromResult(0);
}

public class FakeScheduledJobRepository : IScheduledJobRepository
{
    // Gerçek depoda kilit tek bir koşullu UPDATE ile alınıyor; testte o kararı
    // taklit etmek yerine sonucu doğrudan veriyoruz.
    public bool CanAcquire { get; set; } = true;

    public int EnsureCallCount { get; private set; }
    public int AcquireCallCount { get; private set; }
    public int ReleaseCallCount { get; private set; }
    public string? LastResult { get; private set; }

    public Task EnsureJobExistsAsync(string jobName)
    {
        EnsureCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> TryAcquireAsync(string jobName, TimeSpan interval, TimeSpan lease, string owner)
    {
        AcquireCallCount++;
        return Task.FromResult(CanAcquire);
    }

    public Task ReleaseAsync(string jobName, string? result)
    {
        ReleaseCallCount++;
        LastResult = result;
        return Task.CompletedTask;
    }
}

public class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Logs { get; } = new();

    public Task AddAsync(AuditLog log)
    {
        Logs.Add(log);
        return Task.CompletedTask;
    }
}
