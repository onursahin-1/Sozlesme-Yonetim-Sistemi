using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Sys.Domain;

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
    public string Title => _notification.Title;
    public string Message => _notification.Message;
    public DateTime CreatedAt => _notification.CreatedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackgroundHex))]
    [NotifyPropertyChangedFor(nameof(TitleWeight))]
    [NotifyPropertyChangedFor(nameof(UnreadStripHex))]
    public partial bool IsRead { get; set; }

    // Okunmamış bildirimler hafif mavi zeminle, kalın başlıkla ve sol kenardaki
    // mavi şeritle öne çıkar. Okunmuşlarda şerit şeffaf kalıp yer kaplamaya devam
    // eder; böylece satırlar birbirine göre kaymaz.
    public string RowBackgroundHex => IsRead ? "#FFFFFF" : "#F2F7FC";
    public string TitleWeight => IsRead ? "Normal" : "Bold";
    public string UnreadStripHex => IsRead ? "#00FFFFFF" : "#2D6EA8";

    public string TimeText
    {
        get
        {
            var fark = DateTime.Now - CreatedAt;
            if (fark.TotalMinutes < 1) return "az önce";
            if (fark.TotalMinutes < 60) return $"{(int)fark.TotalMinutes} dk önce";
            if (fark.TotalHours < 24) return $"{(int)fark.TotalHours} saat önce";
            if (fark.TotalDays < 7) return $"{(int)fark.TotalDays} gün önce";
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
        NotificationType.YaklasanBitis => "#B06A00",
        NotificationType.OnayBekliyor => "#2D6EA8",
        NotificationType.TalepSonucu => "#5B6472",
        NotificationType.SozlesmeOlayi => "#93A4BC",
        NotificationType.SifreSifirlamaTalebi => "#A32D2D",
        _ => "#93A4BC"
    };
}
