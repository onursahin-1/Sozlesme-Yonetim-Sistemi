using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class NotificationRepository : INotificationRepository
{
    private readonly string _connectionString;

    public NotificationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Notification>> GetForUserAsync(int userId, int take)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkReadAsync(int notificationId, int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        // UserId koşulu, bir kullanıcının başkasının bildirimini işaretlemesini engeller.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification is null || notification.IsRead) return;

        notification.IsRead = true;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        if (unread.Count == 0) return;

        foreach (var n in unread)
            n.IsRead = true;

        await db.SaveChangesAsync();
    }

    public async Task DeleteReadAsync(int notificationId, int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        // UserId koşulu başkasının bildirimini silmeyi, IsRead koşulu ise okunmamış bir
        // bildirimin (örn. arada gelen yeni bildirimin) yanlışlıkla silinmesini engeller.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && n.IsRead);
        if (notification is null) return;

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
    }

    public async Task<int> DeleteAllReadAsync(int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var read = await db.Notifications
            .Where(n => n.UserId == userId && n.IsRead)
            .ToListAsync();
        if (read.Count == 0) return 0;

        db.Notifications.RemoveRange(read);
        await db.SaveChangesAsync();
        return read.Count;
    }

    public async Task<int> AddManyAsync(List<Notification> notifications)
    {
        if (notifications.Count == 0) return 0;

        using var db = DbConnectionFactory.CreateContext(_connectionString);

        // DedupeKey'i dolu olanlar için, aynı kullanıcı+anahtar ikilisi zaten varsa atlanır.
        // Böylece saat başı çalışan tarama aynı uyarıyı tekrar tekrar üretmez.
        var keyed = notifications.Where(n => n.DedupeKey != null).ToList();
        var toInsert = notifications.Where(n => n.DedupeKey == null).ToList();

        if (keyed.Count > 0)
        {
            var userIds = keyed.Select(n => n.UserId).Distinct().ToList();
            var keys = keyed.Select(n => n.DedupeKey!).Distinct().ToList();

            var existing = await db.Notifications
                .AsNoTracking()
                .Where(n => userIds.Contains(n.UserId) && n.DedupeKey != null && keys.Contains(n.DedupeKey))
                .Select(n => new { n.UserId, n.DedupeKey })
                .ToListAsync();

            var existingSet = existing
                .Select(e => $"{e.UserId}|{e.DedupeKey}")
                .ToHashSet();

            toInsert.AddRange(keyed.Where(n => !existingSet.Contains($"{n.UserId}|{n.DedupeKey}")));
        }

        if (toInsert.Count == 0) return 0;

        db.Notifications.AddRange(toInsert);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Aynı anda çalışan iki örnek aynı bildirimi üretmeye çalışırsa unique index
            // devreye girer. Bildirim kritik bir kayıt olmadığı için sessizce geçilir.
            return 0;
        }

        return toInsert.Count;
    }
}
