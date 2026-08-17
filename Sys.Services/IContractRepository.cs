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
    Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog);
    Task<List<Contract>> GetByStageAsync(int stage);
    Task ApplyEditAsync(Contract contract, ContractRevision revision, AuditLog auditLog);
    Task ApplyViolationAsync(Contract contract, Violation violation, AuditLog auditLog);
    Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination, AuditLog auditLog);
    Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold);
    Task AddAuditLogAsync(AuditLog log);
    Task<List<string>> GetAuditLogUserOptionsAsync();
    Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate);
    Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses);
    Task<Dictionary<ContractStatus, int>> GetStatusCountsAsync(int? createdByUserId);
}