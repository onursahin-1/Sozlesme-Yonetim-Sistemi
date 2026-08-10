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

    public Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
        => Task.CompletedTask;

    public Task ApplyDecisionAsync(Contract contract, ApprovalLog log)
    {
        LastAppliedContract = contract;
        LastAppliedLog = log;
        return Task.CompletedTask;
    }

    public Task ApplyEditAsync(Contract contract, ContractRevision revision) => Task.CompletedTask;
    public Task ApplyViolationAsync(Contract contract, Violation violation) => Task.CompletedTask;
    public Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination) => Task.CompletedTask;
    public Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold) => Task.FromResult(0);
}