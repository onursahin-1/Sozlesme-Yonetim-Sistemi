using Sys.Domain;

namespace Sys.UI;

public static class ContractStatusHelper
{
    public static string ToLabel(ContractStatus status) => status switch
    {
        ContractStatus.Talep => "Talep",
        ContractStatus.OnayBekliyor => "Onay Bekliyor",
        ContractStatus.Aktif => "Aktif",
        ContractStatus.Uyari => "Bitiş Yaklaşıyor",
        ContractStatus.Ihlal => "İhlal Mevcut",
        ContractStatus.Tamamlandi => "Tamamlandı",
        ContractStatus.Feshedildi => "Feshedildi",
        ContractStatus.Reddedildi => "Reddedildi",
        _ => status.ToString()
    };
}