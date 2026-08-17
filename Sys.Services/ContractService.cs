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
    private async Task LogAuditAsync(int contractId, string action, int actingUserId, string? detail)
    {
        var log = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contractId,
            Action = action,
            ActingUserId = actingUserId,
            Detail = detail,
            ActionDate = DateTime.Now,
        };
        await _contracts.AddAuditLogAsync(log);
    }
    public async Task<DashboardStats> GetDashboardStatsAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var counts = await _contracts.GetStatusCountsAsync(userId);
        return new DashboardStats
        {
            Aktif = counts.GetValueOrDefault(ContractStatus.Aktif),
            OnayBekliyor = counts.GetValueOrDefault(ContractStatus.OnayBekliyor),
            Uyari = counts.GetValueOrDefault(ContractStatus.Uyari),
            Ihlal = counts.GetValueOrDefault(ContractStatus.Ihlal),
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
        await LogAuditAsync(contract.Id, "TalepOluşturuldu", contract.CreatedByUserId, $"{contract.Title} için yeni talep oluşturuldu.");
        return contract;
    }
    public async Task UpdateRequestAsync(Contract contract, User actingUser)
    {
        var existing = await GetContractDetailAsync(contract.Id, actingUser);
        if (existing is null)
            throw new InvalidOperationException("Bu talebi düzenleme yetkiniz yok.");
        if (existing.Status != ContractStatus.Talep)
            throw new InvalidOperationException("Bu talep artık düzenlenemez, işlem görmüş.");
        await _contracts.UpdateRequestAsync(contract);
        await LogAuditAsync(contract.Id, "TalepGüncellendi", actingUser.Id, $"{contract.Title} talebi düzenlenip yeniden gönderildi.");
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
            StepName = contract.PendingTermination ? stepName + " (Fesih)" : contract.PendingEdit ? stepName + " (Düzenleme)" : stepName,
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
                    contract.PreviousStatusBeforeTermination = null;
                }
            }
            else
            {
                // Fesih talebi reddedildi — sözleşme fesih öncesi durumuna (Aktif veya Uyarı) döner
                contract.Stage = 3;
                contract.Status = contract.PreviousStatusBeforeTermination ?? ContractStatus.Aktif;
                contract.PendingTermination = false;
                contract.PreviousStatusBeforeTermination = null;
            }
        }
        else if (contract.PendingEdit)
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
                    contract.PendingEdit = false;
                    contract.PreviousStatusBeforeEdit = null;
                }
            }
            else
            {
                // Düzenleme talebi reddedildi — sözleşme düzenleme öncesi durumuna döner, Talep'e düşmez.
                // Bedel ve bitiş tarihi de son revizyondaki eski değerlere geri alınır; aksi halde
                // onaylanmamış değişiklik sözleşmede kalıcı olarak kalırdı.
                var lastRevision = contract.Revisions
                    .OrderByDescending(r => r.ChangedAt)
                    .FirstOrDefault();
                if (lastRevision is not null)
                {
                    contract.TotalAmount = lastRevision.PreviousTotalAmount;
                    contract.EndDate = lastRevision.PreviousEndDate;
                }
                contract.Stage = 3;
                contract.Status = contract.PreviousStatusBeforeEdit ?? ContractStatus.Aktif;
                contract.PendingEdit = false;
                contract.PreviousStatusBeforeEdit = null;
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
                    contract.WasRejected = true;
                    contract.LastRejectionNote = note;
                    contract.LastRejectedAt = DateTime.Now;
                }
                else
                {
                    contract.Stage = 1;
                }
            }
        }
        // Audit log, ApplyDecisionAsync içinde sözleşme/onay kaydıyla aynı transaction'da
        // yazılsın diye burada oluşturulup repository'ye birlikte gönderiliyor.
        var auditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = decision == ApprovalDecision.Onay ? "Onaylandı" : "Reddedildi",
            ActingUserId = actingUser.Id,
            Detail = $"{stepName} - {contract.Title}" + (string.IsNullOrWhiteSpace(note) ? "" : $" - Not: {note}"),
            ActionDate = DateTime.Now,
        };
        await _contracts.ApplyDecisionAsync(contract, log, auditLog);
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
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetByStatusesAsync(userId, ContractStatus.Aktif, ContractStatus.Uyari, ContractStatus.Ihlal);
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
        contract.PreviousStatusBeforeEdit = contract.Status;
        contract.PendingEdit = true;
        contract.Stage = 1;
        contract.Status = ContractStatus.OnayBekliyor;
        var editAuditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = "SözleşmeDüzenlendi",
            ActingUserId = actingUser.Id,
            Detail = $"{contract.Title} - {changeType} - {reason}",
            ActionDate = DateTime.Now,
        };
        await _contracts.ApplyEditAsync(contract, revision, editAuditLog);
    }
    public async Task<List<Contract>> GetViolationReportableContractsAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetByStatusesAsync(userId, ContractStatus.Aktif, ContractStatus.Ihlal);
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
        var violationAuditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = "İhlalBildirildi",
            ActingUserId = reporter.Id,
            Detail = $"{contract.Title} - {violationType}: {description}",
            ActionDate = DateTime.Now,
        };
        await _contracts.ApplyViolationAsync(contract, violation, violationAuditLog);
    }
    public async Task<List<Contract>> GetTerminableContractsAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetByStatusesAsync(userId, ContractStatus.Aktif, ContractStatus.Uyari);
    }
    public async Task<List<Contract>> GetArchivedContractsAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetByStatusesAsync(userId, ContractStatus.Tamamlandi, ContractStatus.Feshedildi);
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
        // Fesih reddedilirse sözleşmenin talep anındaki durumuna (Aktif veya Uyarı)
        // geri dönebilmesi için mevcut durum burada saklanır.
        contract.PreviousStatusBeforeTermination = contract.Status;
        contract.PendingTermination = true;
        contract.Stage = 1;
        contract.Status = ContractStatus.OnayBekliyor;
        var terminationAuditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = "FesihTalebiOluşturuldu",
            ActingUserId = actingUser.Id,
            Detail = $"{contract.Title} - Tür: {terminationType} - Gerekçe: {reason}",
            ActionDate = DateTime.Now,
        };
        await _contracts.ApplyTerminationRequestAsync(contract, termination, terminationAuditLog);
    }
    public async Task<List<string>> GetAuditLogUserOptionsAsync(User currentUser)
    {
        if (currentUser.Role != UserRole.Mudur)
            throw new InvalidOperationException("Bu işlem geçmişini görüntüleme yetkiniz yok.");
        return await _contracts.GetAuditLogUserOptionsAsync();
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(User currentUser, int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate)
    {
        if (currentUser.Role != UserRole.Mudur)
            throw new InvalidOperationException("Bu işlem geçmişini görüntüleme yetkiniz yok.");
        return await _contracts.GetAuditLogsPagedAsync(page, pageSize, userText, startDate, endDate);
    }
}