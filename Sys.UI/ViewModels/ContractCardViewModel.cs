using System;
using Sys.Domain;

namespace Sys.UI.ViewModels;

public class ContractCardViewModel
{
    private readonly Contract _contract;

    public ContractCardViewModel(Contract contract)
    {
        _contract = contract;
    }

    public int Id => _contract.Id;
    public string Title => _contract.Title;
    public string CompanyName => _contract.CompanyName;
    public string RequestRefNo => _contract.RequestRefNo;
    public string Type => _contract.Type;
    public ContractStatus Status => _contract.Status;

    public int Stage => _contract.Stage;

    public string StatusLabel => Status switch
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

    public string StatusColorHex => Status switch
    {
        ContractStatus.Aktif => "#1A6B2A",
        ContractStatus.OnayBekliyor => "#2D6EA8",
        ContractStatus.Uyari => "#B06A00",
        ContractStatus.Ihlal => "#A32D2D",
        ContractStatus.Tamamlandi => "#888888",
        ContractStatus.Feshedildi => "#A32D2D",
        _ => "#555555"
    };

    public string StatusBgHex => Status switch
    {
        ContractStatus.Aktif => "#E6F4E7",
        ContractStatus.OnayBekliyor => "#D6E9F8",
        ContractStatus.Uyari => "#FFF3CD",
        ContractStatus.Ihlal => "#FDECEA",
        ContractStatus.Tamamlandi => "#EAECF0",
        ContractStatus.Feshedildi => "#FDECEA",
        _ => "#EAECF0"
    };

    public string BedelText => _contract.TotalAmount.ToString("N0") + " TL";

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