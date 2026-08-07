using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class ContractRepository : IContractRepository
{
    private readonly SysDbContext _db;

    public ContractRepository(SysDbContext db)
    {
        _db = db;
    }

    public Task<List<Contract>> GetAllAsync()
        => _db.Contracts.ToListAsync();

    public Task<List<Contract>> GetByCreatedUserAsync(int userId)
        => _db.Contracts.Where(c => c.CreatedByUserId == userId).ToListAsync();

    public Task<Contract?> GetByIdWithDetailsAsync(int id)
        => _db.Contracts
            .Include(c => c.Items)
            .Include(c => c.Attachments)
            .Include(c => c.ApprovalLogs)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(Contract contract)
    {
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
    }

    public async Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
    {
        foreach (var item in items)
        {
            item.ContractId = contract.Id;
            _db.ContractItems.Add(item);
        }

        foreach (var attachment in attachments)
        {
            attachment.ContractId = contract.Id;
            _db.Attachments.Add(attachment);
        }

        _db.AuditLogs.Add(auditLog);
        _db.Contracts.Update(contract);

        await _db.SaveChangesAsync();
    }

    public async Task ApplyDecisionAsync(Contract contract, ApprovalLog log)
    {
        log.ContractId = contract.Id;
        _db.ApprovalLogs.Add(log);
        _db.Contracts.Update(contract);
        await _db.SaveChangesAsync();
    }

    public Task<List<Contract>> GetByStageAsync(int stage) => _db.Contracts.Where(c => c.Stage == stage).ToListAsync();
    public async Task ApplyEditAsync(Contract contract, ContractRevision revision)
    {
        revision.ContractId = contract.Id;
        _db.ContractRevisions.Add(revision);
        _db.Contracts.Update(contract);
        await _db.SaveChangesAsync();
    }
    public async Task ApplyViolationAsync(Contract contract, Violation violation)
    {
        _db.Violations.Add(violation);
        _db.Contracts.Update(contract);
        await _db.SaveChangesAsync();
    }
}