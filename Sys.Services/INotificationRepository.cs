using Sys.Domain;

namespace Sys.Services;

public interface INotificationRepository
{
    Task<List<Notification>> GetForUserAsync(int userId, int take);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkReadAsync(int notificationId, int userId);
    Task MarkAllReadAsync(int userId);

    // DedupeKey'i dolu olan bildirimlerden veritabanında zaten var olanlar atlanır.
    // Geriye gerçekten eklenen kayıt sayısı döner.
    Task<int> AddManyAsync(List<Notification> notifications);

    // Silme işlemleri yalnızca okunmuş bildirimler için yapılır; okunmamış bir bildirimin
    // yanlışlıkla silinip gözden kaçması istenmez.
    Task DeleteReadAsync(int notificationId, int userId);
    Task<int> DeleteAllReadAsync(int userId);
}
