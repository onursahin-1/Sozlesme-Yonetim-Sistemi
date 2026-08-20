namespace Sys.Domain;

public enum NotificationType
{
    // Sözleşmenin bitişine az kaldı (30/15/7 gün eşikleri)
    YaklasanBitis,
    // Bir sözleşme bu kullanıcının onay aşamasına düştü
    OnayBekliyor,
    // Kullanıcının kendi talebi onaylandı veya reddedildi
    TalepSonucu,
    // Sözleşme üzerinde düzenleme / fesih talebi / ihlal bildirimi yapıldı
    SozlesmeOlayi
}

public class Notification
{
    public int Id { get; set; }

    // Bildirimin gösterileceği kullanıcı
    public int UserId { get; set; }
    public User? User { get; set; }

    // Bildirime tıklandığında açılacak sözleşme (yoksa null)
    public int? ContractId { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    // Tekrarlayan taramaların (örn. saat başı çalışan "yaklaşan bitiş" kontrolü) aynı
    // olay için defalarca bildirim üretmesini engeller. Örn. "bitis:42:30" — 42 numaralı
    // sözleşmenin 30 günlük uyarısı. (UserId, DedupeKey) üzerinde filtreli unique index var.
    // Olay anında üretilen (onay, red, fesih vb.) bildirimlerde null bırakılır; onlar
    // zaten her seferinde yeni bir olayı temsil eder.
    public string? DedupeKey { get; set; }
}
