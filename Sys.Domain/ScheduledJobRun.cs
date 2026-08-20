namespace Sys.Domain;

// Zamanlanmış bir işin (örn. saatlik bakım) en son ne zaman ve kim tarafından
// çalıştırıldığını tutar. Her iş için tabloda tek bir satır bulunur.
//
// Neden gerekli? Saatlik bakım her kullanıcının kendi bilgisayarında çalışıyordu;
// beş kişi uygulamayı açtığında aynı iş beş kez yapılıyordu. Bu tablo, işi aynı
// saat diliminde yalnızca bir istemcinin üstlenmesini sağlar.
public class ScheduledJobRun
{
    public int Id { get; set; }

    // İşin sabit adı (örn. "HourlyMaintenance"). Üzerinde unique index var.
    public string JobName { get; set; } = string.Empty;

    // İşin en son başarıyla tamamlandığı an. Bir sonraki koşunun zamanı buna göre belirlenir.
    public DateTime? LastRunAt { get; set; }

    // Kilit süresi (lease). İşi üstlenen istemci bu ana kadar sahibidir; çökerse
    // süre dolduğunda kilit kendiliğinden serbest kalır ve iş sonsuza dek asılı kalmaz.
    public DateTime? LockedUntil { get; set; }

    // Kilidi hangi makinenin aldığı — sorun ayıklamayı kolaylaştırır.
    public string? LockedBy { get; set; }

    // Son koşunun kısa özeti (örn. "3 sözleşme güncellendi, 7 bildirim üretildi").
    public string? LastResult { get; set; }
}
