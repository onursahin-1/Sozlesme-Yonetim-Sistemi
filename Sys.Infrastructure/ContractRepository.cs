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

    // Contract üzerindeki "durum makinesi" alanlarını tek yerden kopyalar.
    // Yeni bir durum alanı (örn. ileride eklenecek bir PendingX bayrağı) eklenirse
    // sadece burası güncellenir; ApplyDecisionAsync/ApplyEditAsync/ApplyViolationAsync/
    // ApplyTerminationRequestAsync/FinalizeCreationAsync'in hepsi otomatik senkron kalır.
    private static void CopyWorkflowState(Contract source, Contract tracked)
    {
        tracked.Status = source.Status;
        tracked.Stage = source.Stage;
        tracked.PendingTermination = source.PendingTermination;
        tracked.PendingEdit = source.PendingEdit;
        tracked.PreviousStatusBeforeEdit = source.PreviousStatusBeforeEdit;
        tracked.PreviousStatusBeforeTermination = source.PreviousStatusBeforeTermination;
        tracked.WasRejected = source.WasRejected;
        tracked.LastRejectionNote = source.LastRejectionNote;
        tracked.LastRejectedAt = source.LastRejectedAt;
        // Düzenleme (edit) reddedildiğinde ContractService bedeli/bitiş tarihini eski
        // revizyon değerlerine geri alıyor; bu geri alma işleminin kalıcı olması için
        // bu iki alan da durum makinesiyle birlikte senkron kopyalanır.
        tracked.TotalAmount = source.TotalAmount;
        tracked.EndDate = source.EndDate;
    }

    // Concurrency token uyuşmazlığında (iki kullanıcı aynı sözleşmeyi aynı anda
    // işleme aldığında) ham DbUpdateConcurrencyException yerine, üst katmanların
    // (ContractService/ViewModel) zaten bildiği InvalidOperationException/ErrorMessage
    // deseniyle uyumlu, anlaşılır bir hata fırlatır.
    private static async Task SaveWithConcurrencyCheckAsync(DbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException(
                "Bu sözleşme sizden önce başka bir kullanıcı tarafından güncellendi. " +
                "Lütfen sayfayı yenileyip tekrar deneyin.", ex);
        }
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
            .Include(c => c.CreatedByUser)
            .Include(c => c.Items)
            .Include(c => c.Attachments)
            .Include(c => c.ApprovalLogs)
            .Include(c => c.Terminations)
            .Include(c => c.Revisions)
            .Include(c => c.Violations)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Contract contract)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
    }

    public async Task UpdateRequestAsync(Contract contract)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.RequestRefNo = contract.RequestRefNo;
        tracked.Title = contract.Title;
        tracked.Type = contract.Type;
        tracked.Description = contract.Description;
        tracked.CompanyName = contract.CompanyName;
        tracked.TaxNo = contract.TaxNo;
        tracked.SapCariKodu = contract.SapCariKodu;
        tracked.CompanyType = contract.CompanyType;
        tracked.TotalAmount = contract.TotalAmount;
        // Yeniden gönderilen bir talepte red bilgisi kasıtlı olarak sıfırlanır
        // (kaynaktan kopyalanmıyor) — bu yüzden CopyWorkflowState burada kullanılmıyor.
        tracked.WasRejected = false;
        tracked.LastRejectionNote = null;
        tracked.LastRejectedAt = null;

        await db.SaveChangesAsync();
    }

    public async Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.TotalAmount = contract.TotalAmount;
        tracked.StartDate = contract.StartDate;
        tracked.EndDate = contract.EndDate;
        tracked.PaymentPeriod = contract.PaymentPeriod;
        tracked.SapCariKodu = contract.SapCariKodu;
        tracked.CompanyType = contract.CompanyType;
        CopyWorkflowState(contract, tracked);

        if (string.IsNullOrEmpty(tracked.ContractNo))
        {
            var prefix = $"SYBSA{DateTime.Now:MMyyyy}";
            var lastNo = await db.Contracts
                .Where(c => c.ContractNo != null && c.ContractNo.StartsWith(prefix))
                .OrderByDescending(c => c.ContractNo)
                .Select(c => c.ContractNo)
                .FirstOrDefaultAsync();

            var next = 1;
            if (lastNo is not null && int.TryParse(lastNo.Substring(prefix.Length), out var parsed))
                next = parsed + 1;

            tracked.ContractNo = prefix + next.ToString("D4");
        }

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

    public async Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        CopyWorkflowState(contract, tracked);
        // Kullanıcının ekranda gördüğü RowVersion ile veritabanındaki güncel değer
        // eşleşmiyorsa (araya başka bir güncelleme girmişse) SaveChangesAsync bir
        // DbUpdateConcurrencyException fırlatır — böylece iki kişi aynı sözleşmeyi
        // aynı anda onaylayıp birbirinin işlemini fark etmeden ezemez.
        db.Entry(tracked).Property(c => c.RowVersion).OriginalValue = contract.RowVersion;

        log.ContractId = contract.Id;
        db.ApprovalLogs.Add(log);

        // Audit log, sözleşme durumu ve onay kaydıyla aynı SaveChangesAsync çağrısında
        // (dolayısıyla aynı transaction'da) yazılır — biri başarısız olursa ikisi de
        // geri alınır, yarım kalmış/kayıtsız bir işlem oluşmaz.
        db.AuditLogs.Add(auditLog);

        await SaveWithConcurrencyCheckAsync(db);
    }

    public async Task<List<Contract>> GetByStageAsync(int stage)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts.AsNoTracking().Where(c => c.Stage == stage).ToListAsync();
    }

    public async Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = db.Contracts.AsNoTracking().Where(c => statuses.Contains(c.Status));
        if (createdByUserId.HasValue)
            query = query.Where(c => c.CreatedByUserId == createdByUserId.Value);
        return await query.ToListAsync();
    }

    public async Task<Dictionary<ContractStatus, int>> GetStatusCountsAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = db.Contracts.AsNoTracking().AsQueryable();
        if (createdByUserId.HasValue)
            query = query.Where(c => c.CreatedByUserId == createdByUserId.Value);

        return await query
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
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

    public async Task ApplyEditAsync(Contract contract, ContractRevision revision, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        tracked.TotalAmount = contract.TotalAmount;
        tracked.EndDate = contract.EndDate;
        CopyWorkflowState(contract, tracked);
        db.Entry(tracked).Property(c => c.RowVersion).OriginalValue = contract.RowVersion;

        revision.ContractId = contract.Id;
        db.ContractRevisions.Add(revision);
        db.AuditLogs.Add(auditLog);

        await SaveWithConcurrencyCheckAsync(db);
    }

    public async Task ApplyViolationAsync(Contract contract, Violation violation, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        CopyWorkflowState(contract, tracked);
        db.Entry(tracked).Property(c => c.RowVersion).OriginalValue = contract.RowVersion;

        db.Violations.Add(violation);
        db.AuditLogs.Add(auditLog);

        await SaveWithConcurrencyCheckAsync(db);
    }

    public async Task ApplyTerminationRequestAsync(Contract contract, ContractTermination termination, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        CopyWorkflowState(contract, tracked);
        db.Entry(tracked).Property(c => c.RowVersion).OriginalValue = contract.RowVersion;

        db.ContractTerminations.Add(termination);
        db.AuditLogs.Add(auditLog);

        await SaveWithConcurrencyCheckAsync(db);
    }

    public async Task AddAuditLogAsync(AuditLog log)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task<List<string>> GetAuditLogUserOptionsAsync()
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.AuditLogs
            .AsNoTracking()
            .Select(a => a.ActingUser != null ? a.ActingUser.FullName : ("Kullanıcı #" + a.ActingUserId))
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = db.AuditLogs.AsNoTracking().Include(a => a.ActingUser).AsQueryable();

        if (!string.IsNullOrEmpty(userText))
            query = query.Where(a => (a.ActingUser != null ? a.ActingUser.FullName : ("Kullanıcı #" + a.ActingUserId)) == userText);

        if (startDate.HasValue)
            query = query.Where(a => a.ActionDate.Date >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(a => a.ActionDate.Date <= endDate.Value.Date);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.ActionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}