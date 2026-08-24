using Sys.Domain;

namespace Sys.Services;

public interface IContractRepository
{
    Task<List<Contract>> GetAllAsync();
    Task<List<Contract>> GetByCreatedUserAsync(int userId);
    Task AddAsync(Contract contract);
    Task UpdateRequestAsync(Contract contract);
    Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog);
    Task<Contract?> GetByIdWithDetailsAsync(int id);
    // resolvedRevision / resolvedTermination: karar bir düzenleme ya da fesih talebine
    // aitse, o talebin sonucu (IsApproved/ResolvedAt) sözleşme ve onay kaydıyla AYNI
    // transaction içinde yazılsın diye buraya geçirilir. Ayrı bir çağrıyla yazılsaydı
    // karar kaydedilip sonuç yazılamadığında geçmiş yine tutarsız kalırdı.
    Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog,
                            ContractRevision? resolvedRevision = null,
                            ContractTermination? resolvedTermination = null);
    Task<List<Contract>> GetByStageAsync(int stage);

    // Onay kuyruğu için sayfalanmış hâli; en eski bekleyen üstte.
    Task<(List<Contract> Items, int TotalCount)> GetByStagePagedAsync(int stage, int page, int pageSize);
    Task ApplyEditAsync(Contract contract, ContractRevision revision, AuditLog auditLog);
    Task ApplyViolationAsync(Contract contract, Violation violation, AuditLog auditLog);

    // İhlalin giderilmesi: ihlal kaydının çözüm alanları ve (gerekiyorsa değişen)
    // sözleşme durumu aynı transaction içinde yazılır.
    Task ResolveViolationAsync(Contract contract, Violation violation, AuditLog auditLog);
    Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination, AuditLog auditLog);
    Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold);
    Task AddAuditLogAsync(AuditLog log);
    Task<List<string>> GetAuditLogUserOptionsAsync();
    Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
        int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate, string? action = null);
    Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses);
    Task<Dictionary<ContractStatus, int>> GetStatusCountsAsync(int? createdByUserId);

    // --- Gösterge paneli toplamları ---
    // Hepsi veritabanı tarafında hesaplanır; panel için tüm sözleşmeleri belleğe
    // çekmek gerekmez.

    // Yürürlükteki sözleşmelerin para birimi başına toplam bedeli ve adedi.
    Task<List<CurrencyTotal>> GetActiveValueByCurrencyAsync(int? createdByUserId);

    // Belirtilen ayda açılan talep / yürürlüğe giren / feshedilen sözleşme sayıları.
    Task<MonthlyStats> GetMonthlyStatsAsync(int? createdByUserId, DateTime monthStart, DateTime monthEnd);

    // Önümüzdeki 90 gün içinde biten sözleşmelerin 30 günlük dilimlere dağılımı.
    Task<EndingCalendar> GetEndingCalendarAsync(int? createdByUserId, DateTime today);

    // Yürürlükteki sözleşmelerin türe göre dağılımı (çoktan aza).
    Task<List<TypeCount>> GetTypeBreakdownAsync(int? createdByUserId);

    // Belirli bir aşamada bekleyen sözleşme sayısı ("sizi bekleyen işler" için).
    Task<int> CountByStageAsync(int stage);

    // Belirli durumlardaki sözleşme sayısı.
    Task<int> CountByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses);

    // Reddedilmiş (düzeltme bekleyen) talep sayısı.
    Task<int> CountRejectedRequestsAsync(int? createdByUserId);

    // Sözleşme listesi ekranı için: filtreleme, arama ve sayfalama veritabanı tarafında
    // yapılır. Böylece kayıt sayısı arttığında tüm tablo belleğe çekilmez.
    // includeStatuses: sadece bu durumlar (null = durum filtresi yok)
    // excludeStatuses: bu durumlar hariç (örn. "Tümü" filtresinde Tamamlandı/Feshedildi)
    Task<(List<Contract> Items, int TotalCount)> GetContractsPagedAsync(
        int? createdByUserId,
        ContractStatus[]? includeStatuses,
        ContractStatus[]? excludeStatuses,
        string? searchText,
        int page,
        int pageSize);
}