using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;
using Sys.Services;
using Sys.UI.Localization;

namespace Sys.UI.ViewModels;

// Bildirim listesindeki tek bir satır. Okundu durumu değiştiğinde satırın görünümünün
// (kalınlık/arka plan) anında güncellenmesi için ObservableObject.
public partial class NotificationRowViewModel : ObservableObject
{
    private readonly Notification _notification;

    public NotificationRowViewModel(Notification notification)
    {
        _notification = notification;
        IsRead = notification.IsRead;
    }

    public int Id => _notification.Id;
    public int? ContractId => _notification.ContractId;
    public NotificationType Type => _notification.Type;
    // Metin, bildirimin ÜRETİLDİĞİ anda değil GÖSTERİLDİĞİ anda kuruluyor: aynı
    // bildirim, dil değiştirildiğinde yeni dilde okunuyor.
    //
    // Anahtar yoksa saklanmış hazır metne düşülüyor — bunlar bildirimler anahtara
    // geçmeden önce üretilmiş eski kayıtlar. Geçmişi toplu güncellemek yerine iki
    // biçimin bir arada yaşamasına izin verildi; eski bildirimler zaten kısa
    // ömürlü.
    public string Title => _notification.TitleKey is { } key
        ? Strings.T(key)
        : _notification.Title;

    public string Message
    {
        get
        {
            if (_notification.MessageKey is not { } key) return _notification.Message;

            // Parametrelerin bazıları kendisi de anahtar olabiliyor (konu adı,
            // durum adı). Sözlükte varsa çevrilir, yoksa olduğu gibi kullanılır —
            // sözleşme başlığı ve kullanıcı gerekçesi böyle geçiyor.
            var args = NotificationArgs.Deserialize(_notification.MessageArgs)
                .Select(a => Strings.Has(a) ? Strings.T(a) : a)
                .Cast<object?>()
                .ToArray();

            return args.Length == 0 ? Strings.T(key) : Strings.T(key, args);
        }
    }
    public DateTime CreatedAt => _notification.CreatedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackgroundHex))]
    [NotifyPropertyChangedFor(nameof(TitleWeight))]
    [NotifyPropertyChangedFor(nameof(UnreadStripHex))]
    public partial bool IsRead { get; set; }

    // Okunmamış bildirimler hafif mavi zeminle, kalın başlıkla ve sol kenardaki
    // mavi şeritle öne çıkar. Okunmuşlarda şerit şeffaf kalıp yer kaplamaya devam
    // eder; böylece satırlar birbirine göre kaymaz.
    // Okunmuş bildirim panelin kendi yüzeyinde durur; okunmamış olan hafif
    // vurgulu bir zemin alır.
    public string RowBackgroundHex => IsRead ? "SurfaceCard" : "AccentSoftBg";
    public string TitleWeight => IsRead ? "Normal" : "Bold";
    public string UnreadStripHex => IsRead ? "#00FFFFFF" : "AccentBase";

    public string TimeText
    {
        get
        {
            var fark = DateTime.Now - CreatedAt;
            if (fark.TotalMinutes < 1) return Strings.T("Notif.JustNow");
            if (fark.TotalMinutes < 60) return Strings.T("Notif.MinutesAgo", (int)fark.TotalMinutes);
            if (fark.TotalHours < 24) return Strings.T("Notif.HoursAgo", (int)fark.TotalHours);
            if (fark.TotalDays < 7) return Strings.T("Notif.DaysAgo", (int)fark.TotalDays);
            return CreatedAt.ToString("dd.MM.yyyy HH:mm");
        }
    }

    // Emoji yerine renkli nokta. Emoji her Windows sürümünde farklı çiziliyor,
    // hizası kayıyor ve boyutu satır yüksekliğini bozuyordu — uygulamanın geri
    // kalanında da emojileri vektör/renk göstergelere çevirmiştik.
    //
    // "Talep sonucu" nötr renkte: bildirim hem onayı hem reddi taşıyabiliyor,
    // yeşil bir nokta reddedilen talepte yanıltıcı olurdu. Sonucu başlık söylüyor.
    public string TypeColorHex => _notification.Type switch
    {
        NotificationType.YaklasanBitis => "WarningBase",
        NotificationType.OnayBekliyor => "AccentBase",
        NotificationType.TalepSonucu => "TextLabel",
        NotificationType.SozlesmeOlayi => "TextMutedAlt",
        NotificationType.SifreSifirlamaTalebi => "DangerBase",
        _ => "TextMutedAlt"
    };
}
