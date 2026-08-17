using System;
using Sys.Domain;

namespace Sys.UI.ViewModels;

public class ContractCardViewModel
{
    private readonly Contract _contract;
    private readonly bool _isSyb;

    public ContractCardViewModel(Contract contract, int currentUserId = 0, bool isSyb = false)
    {
        _contract = contract;
        _isSyb = isSyb;
        IsEditable = currentUserId != 0 && contract.CreatedByUserId == currentUserId && contract.Status == ContractStatus.Talep;
    }

    public Contract RawContract => _contract;
    public bool IsEditable { get; }

    // SYB rolündeki kullanıcı için: bu kart "Sözleşme Yarat" işlemini mi bekliyor?
    public bool NeedsContractCreation => _isSyb && Status == ContractStatus.Talep;

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

    public string StatusLabel => Status == ContractStatus.Talep && _contract.WasRejected
        ? "Reddedildi"
        : Status switch
        {
            ContractStatus.Talep => "Talep",
            ContractStatus.OnayBekliyor => "Onay Bekliyor" + StageDetail,
            ContractStatus.Aktif => "Aktif",
            ContractStatus.Uyari => "Bitiş Yaklaşıyor",
            ContractStatus.Ihlal => "İhlal Mevcut",
            ContractStatus.Tamamlandi => "Tamamlandı",
            ContractStatus.Feshedildi => "Feshedildi",
            _ => Status.ToString()
        };

    public bool IsRejected => Status == ContractStatus.Talep && _contract.WasRejected;

    public string RejectionNoteText => string.IsNullOrEmpty(_contract.LastRejectionNote)
        ? "Red gerekçesi belirtilmemiş."
        : "Red gerekçesi: " + _contract.LastRejectionNote;

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

    public string StatusColorHex => IsRejected ? "#A32D2D" : Status switch
    {
        ContractStatus.Aktif => "#1A6B2A",
        ContractStatus.OnayBekliyor => "#2D6EA8",
        ContractStatus.Uyari => "#B06A00",
        ContractStatus.Ihlal => "#A32D2D",
        ContractStatus.Tamamlandi => "#888888",
        ContractStatus.Feshedildi => "#A32D2D",
        _ => "#555555"
    };

    public string StatusBgHex => IsRejected ? "#FDECEA" : Status switch
    {
        ContractStatus.Aktif => "#E6F4E7",
        ContractStatus.OnayBekliyor => "#D6E9F8",
        ContractStatus.Uyari => "#FFF3CD",
        ContractStatus.Ihlal => "#FDECEA",
        ContractStatus.Tamamlandi => "#EAECF0",
        ContractStatus.Feshedildi => "#FDECEA",
        _ => "#EAECF0"
    };

    public string BedelText => _contract.TotalAmount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " TL";

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