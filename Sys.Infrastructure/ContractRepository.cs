using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class ContractRepository : IContractRepository
{
    private readonly string _connectionString;

    public ContractRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Contract>> GetAllAsync()
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts.AsNoTracking().ToListAsync();
    }

    public async Task<List<Contract>> GetByCreatedUserAsync(int userId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts.AsNoTracking().Where(c => c.CreatedByUserId == userId).ToListAsync();
    }

    public async Task<Contract?> GetByIdWithDetailsAsync(int id)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Items)
            .Include(c => c.Attachments)
            .Include(c => c.ApprovalLogs)
            .Include(c => c.Terminations)
            .Include(c => c.Revisions)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Contract contract)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
    }

    public async Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.TotalAmount = contract.TotalAmount;
        tracked.Status = contract.Status;
        tracked.Stage = contract.Stage;

        foreach (var item in items)
        {
            item.ContractId = contract.Id;
            db.ContractItems.Add(item);
        }

        foreach (var attachment in attachments)
        {
            attachment.ContractId = contract.Id;
            db.Attachments.Add(attachment);
        }

        db.AuditLogs.Add(auditLog);

        await db.SaveChangesAsync();
    }

    public async Task ApplyDecisionAsync(Contract contract, ApprovalLog log)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.Stage = contract.Stage;
        tracked.Status = contract.Status;
        tracked.PendingTermination = contract.PendingTermination;

        log.ContractId = contract.Id;
        db.ApprovalLogs.Add(log);

        await db.SaveChangesAsync();
    }

    public async Task<List<Contract>> GetByStageAsync(int stage)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts.AsNoTracking().Where(c => c.Stage == stage).ToListAsync();
    }

    public async Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var candidates = await db.Contracts
            .Where(c => c.Status == ContractStatus.Aktif || c.Status == ContractStatus.Uyari)
            .Where(c => c.EndDate != null)
            .ToListAsync();

        int updated = 0;
        foreach (var c in candidates)
        {
            var newStatus = c.EndDate!.Value.Date < today
                ? ContractStatus.Tamamlandi
                : c.EndDate.Value.Date <= warningThreshold
                    ? ContractStatus.Uyari
                    : ContractStatus.Aktif;

            if (newStatus != c.Status)
            {
                c.Status = newStatus;
                updated++;
            }
        }

        if (updated > 0) await db.SaveChangesAsync();
        return updated;
    }

    public async Task ApplyEditAsync(Contract contract, ContractRevision revision)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.TotalAmount = contract.TotalAmount;
        tracked.EndDate = contract.EndDate;
        tracked.Stage = contract.Stage;
        tracked.Status = contract.Status;

        revision.ContractId = contract.Id;
        db.ContractRevisions.Add(revision);

        await db.SaveChangesAsync();
    }

    public async Task ApplyViolationAsync(Contract contract, Violation violation)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.Status = contract.Status;

        db.Violations.Add(violation);

        await db.SaveChangesAsync();
    }

    public async Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.PendingTermination = contract.PendingTermination;
        tracked.Stage = contract.Stage;
        tracked.Status = contract.Status;

        db.ContractTerminations.Add(termination);

        await db.SaveChangesAsync();
    }
}