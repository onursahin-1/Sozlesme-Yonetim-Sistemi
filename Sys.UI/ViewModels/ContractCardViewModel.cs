using System;
using Sys.Domain;
using Sys.Services;

namespace Sys.UI.ViewModels;

public class ContractCardViewModel
{
    private readonly Contract _contract;
    private readonly bool _isSyb;

    public ContractCardViewModel(Contract contract, int currentUserId = 0, bool isSyb = false, User? currentUser = null)
    {
        _contract = contract;
        _isSyb = isSyb;
        IsEditable = currentUserId != 0 && contract.CreatedByUserId == currentUserId && contract.Status == ContractStatus.Talep;
        CanRenew = currentUser is not null && ContractService.CanRenew(contract, currentUser);
    }

    public Contract RawContract => _contract;
    public bool IsEditable { get; }

    // Yürürlükteki veya süresi dolmuş bir sözleşmeden yeni dönem talebi açılabilir.
    // Feshedilen sözleşme yenilenemez: fesih, tarafların ilişkiyi sürdürmeme kararıdır;
    // yeniden çalışılacaksa bu sıfırdan değerlendirilmesi gereken yeni bir karardır.
    //
    // Yalnızca DETAY ekranlarında kullanılır. Liste kartlarında bilinçli olarak yok:
    // yenileme, önceki dönemin kalemlerine ve koşullarına bakılarak verilen bir karar;
    // listede tek satır bilgiyle başlatılması doğru olmaz. Ayrıca kart üzerindeki
    // buton sayısını da artırırdı.
    public bool CanRenew { get; }

    // SYB rolündeki kullanıcı için: bu kart "Sözleşme Yarat" işlemini mi bekliyor?
    public bool NeedsContractCreation => _isSyb && Status == ContractStatus.Talep;

    // Sözleşmeye dönüşmemiş bir talep, SYB tarafından doğrudan reddedilebilir.
    // Eskiden tek yol talebi önce sözleşmeye çevirip Son Kontrol'de reddetmekti;
    // bu hem gereksiz veri girişi hem de boşa yanan bir sözleşme numarası demekti.
    public bool CanRejectRequest => _isSyb && Status == ContractStatus.Talep;

    // SYB rolündeki kullanıcı için: bu kart "Son Kontrol" (aşama 1 onayı) işlemini mi bekliyor?
    public bool NeedsSybSonKontrol => _isSyb && Status == ContractStatus.OnayBekliyor && Stage == 1;

    public int Id => _contract.Id;
    public string Title => _contract.Title;
    public string CompanyName => _contract.CompanyName;
    public string RequestRefNo => _contract.RequestRefNo;
    public string RefNoText => string.IsNullOrEmpty(_contract.ContractNo) ? _contract.RequestRefNo : _contract.ContractNo!;
    public string Type => _contract.Type;
    public ContractStatus Status => _contract.Status;

    public int Stage => _contract.Stage;

    public string StatusLabel => IsReturned
        ? "İade Edildi"
        : Status switch
        {
            ContractStatus.Talep => "Talep",
            ContractStatus.OnayBekliyor => "Onay Bekliyor" + StageDetail,
            ContractStatus.Aktif => "Aktif",
            ContractStatus.Uyari => "Bitiş Yaklaşıyor",
            ContractStatus.Ihlal => "İhlal Mevcut",
            ContractStatus.Tamamlandi => "Tamamlandı",
            ContractStatus.Feshedildi => "Feshedildi",
            ContractStatus.Reddedildi => "Reddedildi",
            _ => Status.ToString()
        };

    // İade: talep sahibine geri döndü, düzeltilip yeniden gönderilebilir.
    public bool IsReturned => Status == ContractStatus.Talep && _contract.WasRejected;

    // Kapatma: talep nihai olarak reddedildi, yeniden gönderilemez.
    public bool IsClosedRejected => Status == ContractStatus.Reddedildi;

    // Müdür (YK) reddi: sözleşme onay zincirinden çıkmadı, SYB Son Kontrol'e geri
    // döndü. Kart durumu "Onay Bekliyor" olarak kalır ama SYB'nin bunun ikinci bir
    // inceleme olduğunu ve neden geri geldiğini görmesi gerekir.
    public bool IsSentBackToSyb =>
        Status == ContractStatus.OnayBekliyor && Stage == 1 && _contract.WasRejected;

    // Gerekçe kutusu üç durumda da gösterilir.
    public bool IsRejected => IsReturned || IsClosedRejected || IsSentBackToSyb;

    public string RejectionNoteText
    {
        get
        {
            var note = string.IsNullOrWhiteSpace(_contract.LastRejectionNote)
                ? "belirtilmemiş"
                : _contract.LastRejectionNote;

            if (IsClosedRejected)
                return $"Talep reddedildi ve kapatıldı. Gerekçe: {note}";

            if (IsSentBackToSyb)
                return $"Yönetim onayından döndü — Son Kontrol tekrar yapılmalı. Gerekçe: {note}";

            return $"Düzeltilmek üzere iade edildi. Gerekçe: {note}";
        }
    }

    private string StageDetail
    {
        get
        {
            var who = Stage switch
            {
                1 => "SYB Son Kontrol",
                2 => "YK Onayı",
                _ => null
            };

            if (who is null) return string.Empty;

            return _contract.PendingTermination ? $" ({who} — Fesih)" : $" ({who})";
        }
    }

    public string StatusColorHex => IsRejected ? "DangerBase" : Status switch
    {
        ContractStatus.Aktif => "SuccessBase",
        ContractStatus.OnayBekliyor => "AccentBase",
        ContractStatus.Uyari => "WarningBase",
        ContractStatus.Ihlal => "DangerBase",
        ContractStatus.Tamamlandi => "TextFaint",
        ContractStatus.Feshedildi => "DangerBase",
        ContractStatus.Reddedildi => "DangerBase",
        _ => "TextLabel"
    };

    public string StatusBgHex => IsRejected ? "DangerSoftBg" : Status switch
    {
        ContractStatus.Aktif => "SuccessSoftBg",
        ContractStatus.OnayBekliyor => "AccentSoftBorder",
        ContractStatus.Uyari => "WarningSoftBg",
        ContractStatus.Ihlal => "DangerSoftBg",
        ContractStatus.Tamamlandi => "SurfaceDivider",
        ContractStatus.Feshedildi => "DangerSoftBg",
        ContractStatus.Reddedildi => "DangerSoftBg",
        _ => "SurfaceDivider"
    };

    public string BedelText => CurrencyHelper.Format(_contract.TotalAmount, _contract.Currency);

    // Arşiv listesinde her kaydın YANINDA hangi tarihte kapandığı yazsın diye:
    // sona eren/feshedilen sözleşmede bitiş tarihi, reddedilen talepte red tarihi
    // anlamlı olan bilgidir (reddedilen talebin bitiş tarihi genelde hiç girilmemiştir).
    public string ArchiveDateText
    {
        get
        {
            if (Status == ContractStatus.Reddedildi)
                return _contract.LastRejectedAt is { } rejectedAt
                    ? "Reddedildi: " + rejectedAt.ToString("dd.MM.yyyy")
                    : "Reddedildi";

            return _contract.EndDate is { } endDate
                ? "Bitiş: " + endDate.ToString("dd.MM.yyyy")
                : "Tarih girilmemiş";
        }
    }

    public string GunKalanText
    {
        get
        {
            if (_contract.EndDate is null) return "-";
            var days = (_contract.EndDate.Value.Date - DateTime.Today).Days;
            return days > 0 ? $"{days} gün kaldı" : "Sona erdi";
        }
    }
}