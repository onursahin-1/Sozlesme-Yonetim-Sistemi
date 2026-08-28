using Sys.Domain;
using Sys.UI.Localization;

namespace Sys.UI;

public static class ContractStatusHelper
{
    public static string ToLabel(ContractStatus status) => status switch
    {
        ContractStatus.Talep => Strings.T("Card.StatusRequest"),
        ContractStatus.OnayBekliyor => Strings.T("Status.PendingApproval"),
        ContractStatus.Aktif => Strings.T("Status.Live"),
        ContractStatus.Uyari => Strings.T("Card.StatusExpiring"),
        ContractStatus.Ihlal => Strings.T("Card.StatusInViolation"),
        ContractStatus.Tamamlandi => Strings.T("Card.StatusCompleted"),
        ContractStatus.Feshedildi => Strings.T("Card.StatusTerminated"),
        ContractStatus.Reddedildi => Strings.T("Card.StatusRejected"),
        _ => status.ToString()
    };
}