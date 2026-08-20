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
    public string Title => _notification.Title;
    public string Message => _notification.Message;
    public DateTime CreatedAt => _notification.CreatedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowBackgroundHex))]
    [NotifyPropertyChangedFor(nameof(TitleWeight))]
    public partial bool IsRead { get; set; }

    // Okunmamış bildirimler hafif mavi zeminle ve kalın başlıkla öne çıkar.
    public string RowBackgroundHex => IsRead ? "#FFFFFF" : "#EAF2FB";
    public string TitleWeight => IsRead ? "Normal" : "Bold";

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

    public string TypeIcon => _notification.Type switch
    {
        NotificationType.YaklasanBitis => "⏳",
        NotificationType.OnayBekliyor => "📝",
        NotificationType.TalepSonucu => "✅",
        NotificationType.SozlesmeOlayi => "📄",
        _ => "•"
    };
}
