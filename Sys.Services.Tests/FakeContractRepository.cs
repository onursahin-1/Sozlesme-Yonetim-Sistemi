using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class FakeContractRepository : IContractRepository
{
    public Contract? LastAppliedContract { get; private set; }
    public ApprovalLog? LastAppliedLog { get; private set; }

    public Task<List<Contract>> GetAllAsync() => Task.FromResult(new List<Contract>());
    public Task<List<Contract>> GetByCreatedUserAsync(int userId) => Task.FromResult(new List<Contract>());
    public Task<List<Contract>> GetByStageAsync(int stage) => Task.FromResult(new List<Contract>());
    public Task<Contract?> GetByIdWithDetailsAsync(int id) => Task.FromResult<Contract?>(null);
    public Task AddAsync(Contract contract) => Task.CompletedTask;
    public Task UpdateRequestAsync(Contract contract) => Task.CompletedTask;

    public Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
        => Task.CompletedTask;

    public Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog)
    {
        LastAppliedContract = contract;
        LastAppliedLog = log;
        return Task.CompletedTask;
    }

    public Task ApplyEditAsync(Contract contract, ContractRevision revision, AuditLog auditLog) => Task.CompletedTask;
    public Task ApplyViolationAsync(Contract contract, Violation violation, AuditLog auditLog) => Task.CompletedTask;
    public Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination, AuditLog auditLog) => Task.CompletedTask;
    public Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold) => Task.FromResult(0);
    public Task AddAuditLogAsync(AuditLog log) => Task.CompletedTask;
    public Task<List<string>> GetAuditLogUserOptionsAsync() => Task.FromResult(new List<string>());

    public Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate)
        => Task.FromResult((new List<AuditLog>(), 0));

    public Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
        => Task.FromResult(new List<Contract>());

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

    public Task<int> CountByStageAsync(int stage) => Task.FromResult(0);

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