using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class FakeContractRepository : IContractRepository
{
    public Contract? LastAppliedContract { get; private set; }
    public ApprovalLog? LastAppliedLog { get; private set; }

    public int StageCount { get; set; }
    public Task<int> CountByStageAsync(int stage) => Task.FromResult(StageCount);

    public Task<List<Contract>> GetContractsForExportAsync(
        int? createdByUserId, ContractStatus[]? includeStatuses, ContractStatus[]? excludeStatuses,
        string? searchText, int maxRows) => Task.FromResult(new List<Contract>());

    public Task<List<AuditLog>> GetAuditLogsForExportAsync(
        string? userText, DateTime? startDate, DateTime? endDate, string? action, int maxRows)
        => Task.FromResult(new List<AuditLog>());

    public Task<(List<Contract> Items, int TotalCount)> GetByStagePagedAsync(int stage, int page, int pageSize)
        => Task.FromResult((new List<Contract>(), 0));
    public Task<Contract?> GetByIdWithDetailsAsync(int id) => Task.FromResult<Contract?>(null);

    public Task<(string RefNo, DateTime? EndDate)?> GetRenewalSourceSummaryAsync(int id)
        => Task.FromResult<(string, DateTime?)?>(null);

    // Seçici sorguları. Testlerde asıl ilgilenilen şey servisin hangi parametrelerle
    // çağırdığı — özellikle Personel için kullanıcı kısıtının uygulanıp uygulanmadığı.
    public int? LastPickerUserId { get; private set; }
    public string? LastPickerSearchText { get; private set; }
    public int LastPickerTake { get; private set; }
    public List<Contract> PickerResult { get; set; } = new();

    public Task<List<Contract>> GetContractsForPickerAsync(int? createdByUserId, string? searchText, int take)
    {
        LastPickerUserId = createdByUserId;
        LastPickerSearchText = searchText;
        LastPickerTake = take;
        return Task.FromResult(PickerResult);
    }
    public Task AddAsync(Contract contract) => Task.CompletedTask;
    public Task UpdateRequestAsync(Contract contract) => Task.CompletedTask;

    public Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
        => Task.CompletedTask;

    public ContractRevision? LastResolvedRevision { get; private set; }
    public ContractTermination? LastResolvedTermination { get; private set; }

    public Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog,
                                   ContractRevision? resolvedRevision = null,
                                   ContractTermination? resolvedTermination = null)
    {
        LastAppliedContract = contract;
        LastAppliedLog = log;
        LastResolvedRevision = resolvedRevision;
        LastResolvedTermination = resolvedTermination;
        return Task.CompletedTask;
    }

    public Task ApplyEditAsync(Contract contract, ContractRevision revision, AuditLog auditLog) => Task.CompletedTask;
    public Task ApplyViolationAsync(Contract contract, Violation violation, AuditLog auditLog) => Task.CompletedTask;

    public Violation? LastResolvedViolation { get; private set; }

    public Task ResolveViolationAsync(Contract contract, Violation violation, AuditLog auditLog)
    {
        LastAppliedContract = contract;
        LastResolvedViolation = violation;
        return Task.CompletedTask;
    }
    public Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination, AuditLog auditLog) => Task.CompletedTask;
    // Bakım işi testleri için: kaç kaydın güncellendiği ve hata yolunun sınanması.
    public int ReconcileResult { get; set; }
    public bool ThrowOnReconcile { get; set; }
    public int ReconcileCallCount { get; private set; }

    public Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold)
    {
        ReconcileCallCount++;
        if (ThrowOnReconcile) throw new InvalidOperationException("veritabanına ulaşılamadı");
        return Task.FromResult(ReconcileResult);
    }
    public Task AddAuditLogAsync(AuditLog log) => Task.CompletedTask;
    public Task<List<string>> GetAuditLogUserOptionsAsync() => Task.FromResult(new List<string>());

    public Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
        int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate, string? action = null)
        => Task.FromResult((new List<AuditLog>(), 0));

    // Servisin sorguyu hangi durumlarla ve hangi kullanıcı kısıtıyla daralttığı
    // testlerde doğrulanıyor: eskiden bu daraltma veritabanında değil bellekte
    // yapılıyordu ve fark edilmiyordu.
    public int? LastStatusQueryUserId { get; private set; }
    public ContractStatus[]? LastStatusQueryStatuses { get; private set; }

    // Bildirim testleri bu listeden besleniyor; varsayılan boş.
    public List<Contract> StatusQueryResult { get; set; } = new();

    public Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
    {
        LastStatusQueryUserId = createdByUserId;
        LastStatusQueryStatuses = statuses;
        return Task.FromResult(StatusQueryResult);
    }

    public Task<Dictionary<ContractStatus, int>> GetStatusCountsAsync(int? createdByUserId)
        => Task.FromResult(new Dictionary<ContractStatus, int>());

    // --- Gösterge paneli toplamları (testlerde kullanılmıyor, boş dönerler) ---
    public Task<List<CurrencyTotal>> GetActiveValueByCurrencyAsync(int? createdByUserId)
        => Task.FromResult(new List<CurrencyTotal>());

    public Task<MonthlyStats> GetMonthlyStatsAsync(int? createdByUserId, DateTime monthStart, DateTime monthEnd)
        => Task.FromResult(new MonthlyStats());

    public Task<EndingCalendar> GetEndingCalendarAsync(int? createdByUserId, DateTime today)
        => Task.FromResult(new EndingCalendar());

    public Task<List<TypeCount>> GetTypeBreakdownAsync(int? createdByUserId)
        => Task.FromResult(new List<TypeCount>());

    public Task<int> CountByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
        => Task.FromResult(0);

    public Task<int> CountRejectedRequestsAsync(int? createdByUserId) => Task.FromResult(0);

    public Task<(List<Contract> Items, int TotalCount)> GetContractsPagedAsync(
        int? createdByUserId,
        ContractStatus[]? includeStatuses,
        ContractStatus[]? excludeStatuses,
        string? searchText,
        int page,
        int pageSize)
        => Task.FromResult((new List<Contract>(), 0));
}