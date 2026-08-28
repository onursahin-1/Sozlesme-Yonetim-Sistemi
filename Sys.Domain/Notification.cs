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
    SozlesmeOlayi,
    // Giriş ekranından şifre sıfırlama talebi geldi (yalnızca Admin'e gider).
    // EF enum'ları tamsayı olarak sakladığı için yeni değerler her zaman SONA eklenir.
    SifreSifirlamaTalebi
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

    // ESKİ KAYITLAR için hazır metin. Bu alanlar, bildirimler çeviri anahtarına
    // geçmeden önce üretilmiş kayıtlarda dolu; yeni kayıtlarda boş kalıyor.
    // Geçmişi toplu güncellemek yerine iki biçimin bir arada yaşamasına izin
    // verildi — eski bildirim zaten birkaç gün içinde okunup siliniyor.
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // YENİ KAYITLAR için çeviri anahtarı ve parametreleri.
    //
    // Bildirim, denetim kaydından farklı olarak KURUMSAL KAYIT DEĞİL: kimse üç ay
    // sonra bir bildirimin metnine dayanarak karar vermiyor. Bu yüzden "kaydedilmiş
    // olan yazıldığı dilde kalır" kuralı burada geçerli değil; metin gösterim
    // anında, okuyanın dilinde kuruluyor.
    //
    // Servis katmanı yine metin üretmiyor — hata kodlarında olduğu gibi yalnızca
    // "ne oldu"yu söylüyor.
    public string? TitleKey { get; set; }
    public string? MessageKey { get; set; }

    // Mesajdaki {0}, {1}... yerlerine girecek değerler; JSON dizi olarak saklanır.
    // Sözleşme başlığı, gerekçe metni, gün sayısı gibi ÇEVRİLMEYEN veriler.
    public string? MessageArgs { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    // Tekrarlayan taramaların (örn. saat başı çalışan "yaklaşan bitiş" kontrolü) aynı
    // olay için defalarca bildirim üretmesini engeller. Örn. "bitis:42:30" — 42 numaralı
    // sözleşmenin 30 günlük uyarısı. (UserId, DedupeKey) üzerinde filtreli unique index var.
    // Olay anında üretilen (onay, red, fesih vb.) bildirimlerde null bırakılır; onlar
    // zaten her seferinde yeni bir olayı temsil eder.
    public string? DedupeKey { get; set; }
}
