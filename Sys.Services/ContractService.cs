using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sys.Domain;

namespace Sys.Services;

public class DashboardStats
{
    public int Aktif { get; set; }
    public int OnayBekliyor { get; set; }
    public int Uyari { get; set; }
    public int Ihlal { get; set; }
}

public class ContractService
{
    private readonly IContractRepository _contracts;
    private readonly IAttachmentRepository _attachments;

    public ContractService(IContractRepository contracts, IAttachmentRepository attachments)
    {
        _contracts = contracts;
        _attachments = attachments;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(User currentUser)
    {
        var contracts = currentUser.Role == UserRole.Personel
            ? await _contracts.GetByCreatedUserAsync(currentUser.Id)
            : await _contracts.GetAllAsync();

        return new DashboardStats
        {
            Aktif = contracts.Count(c => c.Status == ContractStatus.Aktif),
            OnayBekliyor = contracts.Count(c => c.Status == ContractStatus.OnayBekliyor),
            Uyari = contracts.Count(c => c.Status == ContractStatus.Uyari),
            Ihlal = contracts.Count(c => c.Status == ContractStatus.Ihlal),
        };
    }

    public async Task<List<Contract>> GetContractsAsync(User currentUser)
    {
        return currentUser.Role == UserRole.Personel
            ? await _contracts.GetByCreatedUserAsync(currentUser.Id)
            : await _contracts.GetAllAsync();
    }

    public async Task<Contract> CreateRequestAsync(Contract contract)
    {
        contract.Status = ContractStatus.Talep;
        contract.Stage = 0;
        contract.CreatedAt = DateTime.Now;
        await _contracts.AddAsync(contract);
        return contract;
    }

    public async Task AddAttachmentAsync(Attachment attachment)
    {
        await _attachments.AddAsync(attachment);
    }

    public async Task FinalizeContractAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, User actingUser)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        contract.TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice);
        contract.Status = ContractStatus.OnayBekliyor;
        contract.Stage = 1;

        var auditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = "SözleşmeOluşturuldu",
            ActingUserId = actingUser.Id,
            Detail = $"{contract.Title} sözleşmesi SYB tarafından oluşturuldu.",
            ActionDate = DateTime.Now,
        };

        await _contracts.FinalizeCreationAsync(contract, items, attachments, auditLog);
    }

    public async Task<Contract?> GetContractDetailAsync(int id, User currentUser)
    {
        var contract = await _contracts.GetByIdWithDetailsAsync(id);
        if (contract is null) return null;

        if (currentUser.Role == UserRole.Personel && contract.CreatedByUserId != currentUser.Id)
            return null; // başkasının talebini görmesin

        return contract;
    }

    public async Task DecideApprovalAsync(Contract contract, User actingUser, ApprovalDecision decision, string? note)
    {
        var (stepName, expectedRole) = contract.Stage switch
        {
            1 => ("SYB Son Kontrol", UserRole.SYB),
            2 => ("Müdür (YK) Onayı", UserRole.Mudur),
            _ => throw new InvalidOperationException("Bu aşamada onay/red işlemi yapılamaz.")
        };

        if (actingUser.Role != expectedRole)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        var log = new ApprovalLog
        {
            StepNumber = contract.Stage,
            StepName = contract.PendingTermination ? stepName + " (Fesih)" : stepName,
            ActingUserId = actingUser.Id,
            Decision = decision,
            Note = note,
            ActionDate = DateTime.Now
        };

        if (contract.PendingTermination)
        {
            if (decision == ApprovalDecision.Onay)
            {
                if (contract.Stage == 1)
                {
                    contract.Stage = 2;
                }
                else
                {
                    contract.Stage = 3;
                    contract.Status = ContractStatus.Feshedildi;
                    contract.PendingTermination = false;
                }
            }
            else
            {
                // Fesih talebi reddedildi — sözleşme Aktif olarak devam eder
                contract.Stage = 3;
                contract.Status = ContractStatus.Aktif;
                contract.PendingTermination = false;
            }
        }
        else
        {
            if (decision == ApprovalDecision.Onay)
            {
                if (contract.Stage == 1)
                {
                    contract.Stage = 2;
                }
                else
                {
                    contract.Stage = 3;
                    contract.Status = ContractStatus.Aktif;
                }
            }
            else
            {
                if (contract.Stage == 1)
                {
                    contract.Stage = 0;
                    contract.Status = ContractStatus.Talep;
                }
                else
                {
                    contract.Stage = 1;
                }
            }
        }

        await _contracts.ApplyDecisionAsync(contract, log);
    }

    public async Task<List<Contract>> GetPendingApprovalsAsync(User currentUser)
    {
        int stage = currentUser.Role switch
        {
            UserRole.SYB => 1,
            UserRole.Mudur => 2,
            _ => -1
        };

        if (stage == -1) return new List<Contract>();

        return await _contracts.GetByStageAsync(stage);
    }
    public async Task<List<Contract>> GetEditableContractsAsync(User currentUser)
    {
        var all = await GetContractsAsync(currentUser);
        return all.Where(c => c.Status != ContractStatus.Tamamlandi && c.Status != ContractStatus.Feshedildi).ToList();
    }

    public async Task EditContractAsync(Contract contract, User actingUser, string changeType, string reason, decimal? newTotalAmount, DateTime? newEndDate)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        var revision = new ContractRevision
        {
            ChangeType = changeType,
            Reason = reason,
            PreviousTotalAmount = contract.TotalAmount,
            PreviousEndDate = contract.EndDate,
            PreviousDescription = contract.Description,
            ChangedByUserId = actingUser.Id,
            ChangedAt = DateTime.Now
        };

        if (newTotalAmount.HasValue) contract.TotalAmount = newTotalAmount.Value;
        if (newEndDate.HasValue) contract.EndDate = newEndDate.Value;

        contract.Stage = 1;
        contract.Status = ContractStatus.OnayBekliyor;

        await _contracts.ApplyEditAsync(contract, revision);
    }
    public async Task<List<Contract>> GetViolationReportableContractsAsync(User currentUser)
    {
        var all = await GetContractsAsync(currentUser);
        return all.Where(c => c.Status == ContractStatus.Aktif || c.Status == ContractStatus.Ihlal).ToList();
    }

    public async Task ReportViolationAsync(Contract contract, User reporter, string violationType, DateTime violationDate, string description)
    {
        if (reporter.Role == UserRole.Mudur)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        var violation = new Violation
        {
            ContractId = contract.Id,
            ViolationType = violationType,
            ViolationDate = violationDate,
            Description = description,
            ReportedByUserId = reporter.Id,
            ReportedAt = DateTime.Now
        };

        contract.Status = ContractStatus.Ihlal;

        await _contracts.ApplyViolationAsync(contract, violation);
    }
    public async Task<List<Contract>> GetTerminableContractsAsync(User currentUser)
    {
        var all = await GetContractsAsync(currentUser);
        return all.Where(c => c.Status == ContractStatus.Aktif || c.Status == ContractStatus.Uyari).ToList();
    }

    public async Task<List<Contract>> GetArchivedContractsAsync(User currentUser)
    {
        var all = await GetContractsAsync(currentUser);
        return all.Where(c => c.Status == ContractStatus.Tamamlandi || c.Status == ContractStatus.Feshedildi).ToList();
    }

    public async Task<int> ReconcileContractStatusesAsync()
    {
        var today = DateTime.Today;
        var warningThreshold = today.AddDays(30);
        return await _contracts.ReconcileStatusesAsync(today, warningThreshold);
    }

    public async Task RequestTerminationAsync(Contract contract, User actingUser, string terminationType, DateTime terminationDate, string reason, decimal? compensationAmount, string compensationDirection)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        var termination = new ContractTermination
        {
            ContractId = contract.Id,
            TerminationType = terminationType,
            TerminationDate = terminationDate,
            Reason = reason,
            CompensationAmount = compensationAmount,
            CompensationDirection = compensationDirection,
            RequestedByUserId = actingUser.Id,
            RequestedAt = DateTime.Now
        };

        contract.PendingTermination = true;
        contract.Stage = 1;
        contract.Status = ContractStatus.OnayBekliyor;

        await _contracts.ApplyTerminationRequestAsync(contract, termination);
    }

}