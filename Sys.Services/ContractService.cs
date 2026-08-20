using System;
using System.Collections.Generic;
using System.IO;
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

    // Bildirim bağımlılıkları opsiyonel: verilmezse (örn. birim testlerinde) bildirim
    // üretilmez, iş kuralları aynen çalışmaya devam eder.
    private readonly INotificationRepository? _notifications;
    private readonly IUserRepository? _users;

    public ContractService(
        IContractRepository contracts,
        IAttachmentRepository attachments,
        INotificationRepository? notifications = null,
        IUserRepository? users = null)
    {
        _contracts = contracts;
        _attachments = attachments;
        _notifications = notifications;
        _users = users;
    }

    // Bildirim oluşturma, hiçbir zaman asıl iş akışını bozmamalı: bildirim yazılamazsa
    // (bağlantı hatası vb.) onay/fesih işlemi başarılı sayılmaya devam eder.
    private async Task NotifyAsync(IEnumerable<int> userIds, int contractId, NotificationType type, string title, string message)
    {
        if (_notifications is null) return;

        var list = userIds.Distinct().Select(id => new Notification
        {
            UserId = id,
            ContractId = contractId,
            Type = type,
            Title = title,
            Message = message,
            CreatedAt = DateTime.Now
        }).ToList();

        if (list.Count == 0) return;

        try
        {
            await _notifications.AddManyAsync(list);
        }
        catch
        {
            // Bildirim kritik olmayan bir yan etki; sessizce geçilir.
        }
    }

    private async Task<List<int>> GetActiveUserIdsByRoleAsync(UserRole role)
    {
        if (_users is null) return new List<int>();
        try
        {
            var all = await _users.GetAllAsync();
            return all.Where(u => u.Role == role && !u.IsDisabled).Select(u => u.Id).ToList();
        }
        catch
        {
            return new List<int>();
        }
    }

    // Bir sözleşme onay aşamasına girdiğinde/ilerlediğinde, o aşamadan sorumlu role
    // "onayınızı bekliyor" bildirimi gönderir. Aşama-rol eşlemesi DecideApprovalAsync
    // içindeki kuralla aynıdır (1: SYB, 2: Müdür).
    private async Task NotifyStageOwnersAsync(Contract contract, string konu)
    {
        var role = contract.Stage switch
        {
            1 => (UserRole?)UserRole.SYB,
            2 => UserRole.Mudur,
            _ => null
        };
        if (role is null) return;

        var userIds = await GetActiveUserIdsByRoleAsync(role.Value);
        await NotifyAsync(userIds, contract.Id, NotificationType.OnayBekliyor,
            "Onayınızı bekliyor", $"\"{contract.Title}\" {konu} onayınızı bekliyor.");
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

    // Sözleşme listesi ekranı için sunucu taraflı filtre + arama + sayfalama.
    // filterKey, ekrandaki filtre butonlarının CommandParameter değerleriyle aynıdır.
    // Personel yalnızca kendi oluşturduğu sözleşmeleri görebilir (GetContractsAsync ile aynı kural).
    public async Task<(List<Contract> Items, int TotalCount)> GetContractsPagedAsync(
        User currentUser, string filterKey, string? searchText, int page, int pageSize)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var (include, exclude) = MapFilter(filterKey);
        return await _contracts.GetContractsPagedAsync(userId, include, exclude, searchText, page, pageSize);
    }

    // "Tümü" seçildiğinde kapanmış sözleşmeler (Tamamlandı/Feshedildi) listede gösterilmez —
    // bu ekran devam eden işleri gösterir. Eskiden bu ayıklama ViewModel'de bellekte yapılıyordu.
    private static (ContractStatus[]? Include, ContractStatus[]? Exclude) MapFilter(string filterKey) => filterKey switch
    {
        "aktif" => (new[] { ContractStatus.Aktif }, null),
        "onay_bekliyor" => (new[] { ContractStatus.OnayBekliyor }, null),
        "uyari" => (new[] { ContractStatus.Uyari }, null),
        "ihlal" => (new[] { ContractStatus.Ihlal }, null),
        "tamamlandi" => (new[] { ContractStatus.Tamamlandi }, null),
        _ => (null, new[] { ContractStatus.Tamamlandi, ContractStatus.Feshedildi })
    };

    // Gösterge panelindeki "Yaklaşan Bitişler" kutusu için: belirtilen gün içinde
    // (varsayılan 30) bitecek Aktif/Uyarı durumundaki sözleşmeler, bitiş tarihine
    // göre en yakından uzağa sıralı olarak döner.
    public async Task<List<Contract>> GetUpcomingEndingsAsync(User currentUser, int days = 30, int take = 5)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var contracts = await _contracts.GetByStatusesAsync(userId, ContractStatus.Aktif, ContractStatus.Uyari);
        var today = DateTime.Today;
        var threshold = today.AddDays(days);
        return contracts
            .Where(c => c.EndDate.HasValue && c.EndDate.Value.Date >= today && c.EndDate.Value.Date <= threshold)
            .OrderBy(c => c.EndDate)
            .Take(take)
            .ToList();
    }

    // Gösterge panelindeki "Son Aktiviteler" kutusu için. Bu, kimin ne zaman ne yaptığını
    // isim isim gösterdiği için "İşlem Geçmişi" ekranıyla aynı yetki sınırına tabidir ve
    // yalnızca Müdür'e döndürülür; diğer roller için boş liste döner.
    public async Task<List<AuditLog>> GetRecentActivityAsync(User currentUser, int take = 5)
    {
        if (currentUser.Role != UserRole.Mudur) return new List<AuditLog>();

        var (allItems, _) = await _contracts.GetAuditLogsPagedAsync(1, take, null, null, null);
        return allItems;
    }
    public async Task<Contract> CreateRequestAsync(Contract contract)
    {
        contract.Status = ContractStatus.Talep;
        contract.Stage = 0;
        contract.CreatedAt = DateTime.Now;
        await _contracts.AddAsync(contract);
        await LogAuditAsync(contract.Id, "TalepOluşturuldu", contract.CreatedByUserId, $"{contract.Title} için yeni talep oluşturuldu.");

        // Yeni talep henüz onay aşamasında değil (Stage 0), ama sözleşmeyi oluşturacak
        // olan SYB'nin talepten haberi olmalı — aksi halde listeyi elle taramak gerekir.
        var sybIds = await GetActiveUserIdsByRoleAsync(UserRole.SYB);
        await NotifyAsync(sybIds, contract.Id, NotificationType.SozlesmeOlayi,
            "Yeni sözleşme talebi", $"\"{contract.Title}\" için yeni bir sözleşme talebi oluşturuldu.");

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

        // Genellikle reddedilmiş bir talep düzeltilip yeniden gönderilir; SYB'nin
        // güncellenmiş talebi tekrar ele alması gerektiğini bilmesi lazım.
        var sybIds = await GetActiveUserIdsByRoleAsync(UserRole.SYB);
        await NotifyAsync(sybIds, contract.Id, NotificationType.SozlesmeOlayi,
            "Talep güncellendi", $"\"{contract.Title}\" talebi düzenlenip yeniden gönderildi.");
    }
    public async Task AddAttachmentAsync(Attachment attachment)
    {
        await _attachments.AddAsync(attachment);
    }

    // Ek dosya erişim/indirme/silme işlemleri AuditLog'a "Attachment" varlığı olarak
    // kaydedilir; böylece bir belgeyi kimin ne zaman görüntülediği/indirdiği/sildiği
    // "İşlem Geçmişi" ekranından izlenebilir olur.
    private async Task LogAttachmentAccessAsync(Attachment attachment, string action, User actingUser)
    {
        var log = new AuditLog
        {
            EntityName = "Attachment",
            EntityId = attachment.Id,
            Action = action,
            ActingUserId = actingUser.Id,
            Detail = $"{attachment.FileName} (Sözleşme #{attachment.ContractId})",
            ActionDate = DateTime.Now,
        };
        await _contracts.AddAuditLogAsync(log);
    }

    public Task LogAttachmentOpenedAsync(Attachment attachment, User actingUser)
        => LogAttachmentAccessAsync(attachment, "EkGörüntülendi", actingUser);

    public Task LogAttachmentDownloadedAsync(Attachment attachment, User actingUser)
        => LogAttachmentAccessAsync(attachment, "Ekİndirildi", actingUser);

    // Sözleşme künyesinin PDF olarak dışa aktarılması da denetim kaydına yazılır:
    // sözleşme verisi uygulama dışına çıkmış oluyor, kimin ne zaman aldığı izlenebilmeli.
    public Task LogContractPrintedAsync(Contract contract, User actingUser)
        => LogAuditAsync(contract.Id, "SözleşmeYazdırıldı", actingUser.Id, contract.Title);

    public async Task DeleteAttachmentAsync(Attachment attachment, User actingUser)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        await _attachments.DeleteAsync(attachment.Id);

        try
        {
            if (File.Exists(attachment.FilePath))
                File.Delete(attachment.FilePath);
        }
        catch
        {
            // Fiziksel dosya silinemese bile (örn. başka bir programda açık), veritabanı
            // kaydı silindiği için ekran listesinde artık görünmeyecek — kritik değil.
        }

        await LogAttachmentAccessAsync(attachment, "EkSilindi", actingUser);
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

        // Onay zincirinin BAŞLANGICI burası: sözleşme Stage 1'e (SYB Son Kontrol) taşındı.
        // Bu bildirim olmadan zincir hiç başlamıyor, sonraki aşamaların bildirimleri de
        // dolayısıyla tetiklenmiyordu.
        await NotifyStageOwnersAsync(contract, "sözleşmesi");

        // Talebi açan kişi de sözleşmesinin oluşturulup onaya girdiğini görsün.
        await NotifyAsync(new[] { contract.CreatedByUserId }, contract.Id, NotificationType.SozlesmeOlayi,
            "Sözleşmeniz oluşturuldu", $"\"{contract.Title}\" sözleşmesi oluşturuldu ve onay sürecine girdi.");
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
        if (decision == ApprovalDecision.Red && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Reddetme işlemi için bir gerekçe girilmelidir.");
        // Bildirim metninde kullanılacak konu, aşağıdaki durum değişikliklerinden ÖNCE
        // saklanır: karar uygulandığında PendingEdit/PendingTermination temizlenebiliyor.
        var bildirimKonusu = contract.PendingTermination ? "fesih talebi"
            : contract.PendingEdit ? "düzenleme talebi"
            : "sözleşmesi";
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
                    contract.Description = lastRevision.PreviousDescription;
                    contract.CompanyName = lastRevision.PreviousCompanyName;
                    contract.TaxNo = lastRevision.PreviousTaxNo;
                    contract.PaymentPeriod = lastRevision.PreviousPaymentPeriod;
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

        // Karar kaydedildikten sonra sözleşmenin ULAŞTIĞI aşamaya göre bildirim üretilir.
        if (contract.Stage is 1 or 2)
        {
            // Bir sonraki onay aşamasına geçti (ya da Müdür reddedip SYB'ye geri gönderdi).
            await NotifyStageOwnersAsync(contract, bildirimKonusu);
        }
        else
        {
            // Süreç tamamlandı ya da talep sahibine geri döndü — sonucu talebi açan kişi görsün.
            var (baslik, mesaj) = decision == ApprovalDecision.Onay
                ? ("Talebiniz onaylandı", $"\"{contract.Title}\" {bildirimKonusu} onaylandı.")
                : ("Talebiniz reddedildi", $"\"{contract.Title}\" {bildirimKonusu} reddedildi. Gerekçe: {note}");

            await NotifyAsync(new[] { contract.CreatedByUserId }, contract.Id,
                NotificationType.TalepSonucu, baslik, mesaj);
        }
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
    public async Task EditContractAsync(
        Contract contract, User actingUser, string changeType, string reason,
        decimal? newTotalAmount, DateTime? newEndDate,
        string? newDescription = null, string? newCompanyName = null, string? newTaxNo = null, string? newPaymentPeriod = null)
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
            PreviousCompanyName = contract.CompanyName,
            PreviousTaxNo = contract.TaxNo,
            PreviousPaymentPeriod = contract.PaymentPeriod,
            ChangedByUserId = actingUser.Id,
            ChangedAt = DateTime.Now
        };
        if (newTotalAmount.HasValue) contract.TotalAmount = newTotalAmount.Value;
        if (newEndDate.HasValue) contract.EndDate = newEndDate.Value;
        if (!string.IsNullOrWhiteSpace(newDescription)) contract.Description = newDescription;
        if (!string.IsNullOrWhiteSpace(newCompanyName)) contract.CompanyName = newCompanyName;
        if (!string.IsNullOrWhiteSpace(newTaxNo)) contract.TaxNo = newTaxNo;
        if (!string.IsNullOrWhiteSpace(newPaymentPeriod)) contract.PaymentPeriod = newPaymentPeriod;
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

        // Düzenleme onay sürecine girdi: 1. aşamadan sorumlu SYB'ye ve sözleşme sahibine haber ver.
        await NotifyStageOwnersAsync(contract, "düzenleme talebi");
        await NotifyAsync(new[] { contract.CreatedByUserId }, contract.Id, NotificationType.SozlesmeOlayi,
            "Sözleşmede düzenleme", $"\"{contract.Title}\" sözleşmesinde düzenleme yapıldı ve onaya gönderildi. Gerekçe: {reason}");
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

        // İhlal bir onay süreci başlatmaz ama SYB'nin ve sözleşme sahibinin haberi olmalı.
        var ihlalHedefleri = await GetActiveUserIdsByRoleAsync(UserRole.SYB);
        ihlalHedefleri.Add(contract.CreatedByUserId);
        await NotifyAsync(ihlalHedefleri, contract.Id, NotificationType.SozlesmeOlayi,
            "İhlal bildirildi", $"\"{contract.Title}\" sözleşmesinde ihlal bildirildi ({violationType}).");
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

        await NotifyStageOwnersAsync(contract, "fesih talebi");
        await NotifyAsync(new[] { contract.CreatedByUserId }, contract.Id, NotificationType.SozlesmeOlayi,
            "Fesih talebi", $"\"{contract.Title}\" sözleşmesi için fesih talebi oluşturuldu. Gerekçe: {reason}");
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