using Sys.Domain;

namespace Sys.Services;

// Bildirimlerin okunması/işaretlenmesi ve zamanlanmış "yaklaşan bitiş" taraması burada.
// Olay anında üretilen bildirimler (onay, red, fesih vb.) ContractService içinden,
// ilgili iş kuralı çalıştığı noktada oluşturulur.
public class NotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IContractRepository _contracts;
    private readonly IUserRepository _users;

    // Sözleşme bitişine bu gün sayıları kaldığında uyarı üretilir.
    private static readonly int[] EndingThresholds = { 30, 15, 7 };

    public NotificationService(
        INotificationRepository notifications,
        IContractRepository contracts,
        IUserRepository users)
    {
        _notifications = notifications;
        _contracts = contracts;
        _users = users;
    }

    public Task<List<Notification>> GetForUserAsync(User currentUser, int take = 30)
        => _notifications.GetForUserAsync(currentUser.Id, take);

    public Task<int> GetUnreadCountAsync(User currentUser)
        => _notifications.GetUnreadCountAsync(currentUser.Id);

    public Task MarkReadAsync(User currentUser, int notificationId)
        => _notifications.MarkReadAsync(notificationId, currentUser.Id);

    public Task MarkAllReadAsync(User currentUser)
        => _notifications.MarkAllReadAsync(currentUser.Id);

    public Task DeleteReadAsync(User currentUser, int notificationId)
        => _notifications.DeleteReadAsync(notificationId, currentUser.Id);

    public Task<int> DeleteAllReadAsync(User currentUser)
        => _notifications.DeleteAllReadAsync(currentUser.Id);

    // Uygulama açılışında ve saat başı çalışır. Bitişine 30/15/7 gün kalan Aktif/Uyarı
    // sözleşmeleri için, sözleşmeyi oluşturan kişiye ve tüm SYB kullanıcılarına bildirim
    // üretir. DedupeKey sayesinde aynı eşik için ikinci kez bildirim oluşmaz.
    public async Task<int> GenerateUpcomingEndingNotificationsAsync()
    {
        var contracts = await _contracts.GetByStatusesAsync(null, ContractStatus.Aktif, ContractStatus.Uyari);
        if (contracts.Count == 0) return 0;

        var allUsers = await _users.GetAllAsync();
        var sybUserIds = allUsers
            .Where(u => u.Role == UserRole.SYB && !u.IsDisabled)
            .Select(u => u.Id)
            .ToList();

        var today = DateTime.Today;
        var pending = new List<Notification>();

        foreach (var contract in contracts)
        {
            if (!contract.EndDate.HasValue) continue;

            var daysLeft = (contract.EndDate.Value.Date - today).Days;
            if (daysLeft < 0) continue;

            // Geçilen en küçük eşik seçilir: bitişe 10 gün kalmışsa 15'lik uyarı üretilir,
            // 30'luk zaten daha önce üretilmiştir (DedupeKey ile tekrarı engellenir).
            var threshold = EndingThresholds.Where(t => daysLeft <= t).DefaultIfEmpty(0).Min();
            if (threshold == 0) continue;

            // Metin değil ANAHTAR saklanıyor: bildirimin dili, üretildiği an değil
            // okunduğu an belirleniyor. Bu iş arka planda, kullanıcıdan bağımsız
            // çalışıyor — üretildiği anda "hangi dil" sorusunun cevabı zaten yok.
            var messageKey = daysLeft == 0 ? "Ntf.EndsToday" : "Ntf.EndsInDays";
            var args = daysLeft == 0
                ? NotificationArgs.Serialize([contract.Title])
                : NotificationArgs.Serialize([contract.Title, daysLeft]);

            var targetUserIds = new HashSet<int>(sybUserIds) { contract.CreatedByUserId };

            foreach (var userId in targetUserIds)
            {
                pending.Add(new Notification
                {
                    UserId = userId,
                    ContractId = contract.Id,
                    Type = NotificationType.YaklasanBitis,
                    TitleKey = "Ntf.UpcomingEndTitle",
                    MessageKey = messageKey,
                    MessageArgs = args,
                    CreatedAt = DateTime.Now,
                    DedupeKey = $"bitis:{contract.Id}:{threshold}"
                });
            }
        }

        return await _notifications.AddManyAsync(pending);
    }
}
