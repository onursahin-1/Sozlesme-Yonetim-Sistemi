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
        tracked.FinalCheckSkipped = source.FinalCheckSkipped;
        tracked.PendingTermination = source.PendingTermination;
        tracked.PendingEdit = source.PendingEdit;
        tracked.PreviousStatusBeforeEdit = source.PreviousStatusBeforeEdit;
        tracked.PreviousStatusBeforeTermination = source.PreviousStatusBeforeTermination;
        tracked.WasRejected = source.WasRejected;
        tracked.LastRejectionNote = source.LastRejectionNote;
        tracked.LastRejectedAt = source.LastRejectedAt;
        tracked.LastRejectedStage = source.LastRejectedStage;
        // Düzenleme (edit) reddedildiğinde ContractService bedeli/bitiş tarihini (ve artık
        // kapsam/firma/ödeme koşulları alanlarını da) eski revizyon değerlerine geri alıyor;
        // bu geri alma işleminin kalıcı olması için bu alanlar da durum makinesiyle birlikte
        // senkron kopyalanır. Bir düzenleme onaya gönderildiğinde de (ApplyEditAsync) yeni
        // değerlerin kalıcı hale gelmesini aynı yol sağlar.
        tracked.TotalAmount = source.TotalAmount;
        tracked.EndDate = source.EndDate;
        tracked.Description = source.Description;
        tracked.CompanyName = source.CompanyName;
        tracked.TaxNo = source.TaxNo;
        tracked.PaymentPeriod = source.PaymentPeriod;
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

    // GetAllAsync ve GetByCreatedUserAsync burada duruyordu; ikisi de sınır olmadan
    // sözleşme tablosunun tamamını çekiyordu. Tek çağıranları kaldırıldıktan sonra
    // silindiler — dururlarsa er ya da geç yeniden kullanılırlar.
    // Yerlerine: GetContractsPagedAsync, GetByStatusesAsync, GetContractsForPickerAsync.

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

    // Yenileme bağlantısını göstermek için kaynak sözleşmenin yalnızca kimlik
    // bilgileri gerekiyor. Detay sorgusu yedi tabloyu birden çekiyor; her sözleşme
    // açılışında bir de onun için çalıştırmanın anlamı yok.
    public async Task<(string RefNo, DateTime? EndDate)?> GetRenewalSourceSummaryAsync(int id)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var row = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.ContractNo, c.RequestRefNo, c.EndDate })
            .FirstOrDefaultAsync();

        if (row is null) return null;
        return (string.IsNullOrWhiteSpace(row.ContractNo) ? row.RequestRefNo : row.ContractNo!, row.EndDate);
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
        tracked.LastRejectedStage = null;

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

        // Kalemler EKLENMİYOR, DEĞİŞTİRİLİYOR. Bu adım aynı talep için birden fazla kez
        // çalışabiliyor: sözleşme Son Kontrol'de reddedilince talep tekrar Talep durumuna
        // düşüyor ve SYB "Sözleşme Yarat"ı yeniden çalıştırıyor. Eskiden yeni kalemler
        // eskilerin ÜZERİNE ekleniyordu; sonuçta sözleşme iki kat kalem içeriyor ama
        // TotalAmount yalnızca son girilen kalemlerin toplamı oluyordu. İki değer birbirini
        // tutmadığı için tutarsızlık ekranda fark edilmiyordu.
        var existingItems = await db.ContractItems
            .Where(i => i.ContractId == contract.Id)
            .ToListAsync();

        if (existingItems.Count > 0)
            db.ContractItems.RemoveRange(existingItems);

        foreach (var item in items)
        {
            item.Id = 0;
            item.ContractId = contract.Id;
            db.ContractItems.Add(item);
        }

        foreach (var attachment in attachments)
        {
            attachment.ContractId = contract.Id;
            db.Attachments.Add(attachment);
        }

        db.AuditLogs.Add(auditLog);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // ContractNo üzerindeki unique index, iki kullanıcının eşzamanlı
            // "Sözleşme Yarat" işleminde aynı numarayı üretmesini burada yakalar.
            throw new InvalidOperationException(
                "Sözleşme numarası oluşturulurken bir çakışma oldu (aynı anda başka bir sözleşme " +
                "oluşturulmuş olabilir). Lütfen tekrar deneyin.", ex);
        }
    }

    public async Task ApplyDecisionAsync(Contract contract, ApprovalLog log, AuditLog auditLog,
                                         ContractRevision? resolvedRevision = null,
                                         ContractTermination? resolvedTermination = null)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        CopyWorkflowState(contract, tracked);

        // Düzenleme/fesih talebinin sonucu, sözleşme durumuyla aynı SaveChanges
        // çağrısında yazılır. Yalnızca sonuç alanları güncelleniyor; talebin içeriği
        // (gerekçe, önceki değerler) değişmemeli.
        if (resolvedRevision is not null)
        {
            var revision = await db.ContractRevisions.FirstOrDefaultAsync(r => r.Id == resolvedRevision.Id);
            if (revision is not null)
            {
                revision.IsApproved = resolvedRevision.IsApproved;
                revision.ResolvedAt = resolvedRevision.ResolvedAt;
            }
        }

        if (resolvedTermination is not null)
        {
            var termination = await db.ContractTerminations.FirstOrDefaultAsync(t => t.Id == resolvedTermination.Id);
            if (termination is not null)
            {
                termination.IsApproved = resolvedTermination.IsApproved;
                termination.ResolvedAt = resolvedTermination.ResolvedAt;
            }
        }
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

    // Aşamaya göre sayım için CountByStageAsync kullanılıyor (gösterge paneli
    // bölümünde). Buradaki GetByStageAsync kayıtların TAMAMINI çekiyordu ve tek
    // çağıranı sol menüdeki rozetti — yalnızca bir sayı göstermek için.

    // Onay kuyruğu, diğer listelerin aksine tüm kayıtları tek seferde çekiyordu.
    // En eski bekleyen üstte: onay kuyruğunda sıra beklemede kalma süresine göre
    // olmalı, aksi halde eski talepler listenin dibinde unutulur.
    public async Task<(List<Contract> Items, int TotalCount)> GetByStagePagedAsync(int stage, int page, int pageSize)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = db.Contracts.AsNoTracking().Where(c => c.Stage == stage);

        var totalCount = await query.CountAsync();

        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Contract>> GetByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = db.Contracts.AsNoTracking().Where(c => statuses.Contains(c.Status));
        if (createdByUserId.HasValue)
            query = query.Where(c => c.CreatedByUserId == createdByUserId.Value);
        return await query.ToListAsync();
    }

    // Sözleşme seçici listesi (Görüntüle ekranındaki açılır kutu).
    //
    // Eskiden bu kutu TÜM sözleşmeleri belleğe çekiyordu; kayıt sayısı arttıkça
    // ekranın açılışı yavaşlıyordu ve kullanıcı yüzlerce satır arasında kaydırarak
    // arıyordu. Artık aynı arama ifadesi veritabanına gidiyor ve yalnızca ilk
    // "take" kadar sonuç dönüyor. Sıralama en yeniden eskiye: arama yokken
    // kullanıcının aradığı sözleşme büyük ihtimalle son girilenlerden biridir.
    public async Task<List<Contract>> GetContractsForPickerAsync(int? createdByUserId, string? searchText, int take)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        return await BuildContractQuery(db, createdByUserId, null, null, searchText)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(take)
            .ToListAsync();
    }

    // Tür filtresi açılır listesi. Sabit bir liste yerine VERİDEN besleniyor:
    // sözleşme türü serbest metin olarak da girilebiliyor ve sabit listede olmayan
    // bir tür kaydedilirse filtreyle hiç bulunamaz hâle gelirdi.
    public async Task<List<string>> GetContractTypeOptionsAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var query = db.Contracts.AsNoTracking().Where(c => c.Type != "");
        if (createdByUserId.HasValue)
            query = query.Where(c => c.CreatedByUserId == createdByUserId.Value);

        return await query
            .Select(c => c.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
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

    // Ekrandaki filtrelerin aynısını uygular ama sayfalamaz: Excel'e aktarmanın amacı
    // tüm eşleşen kayıtları analiz edebilmek. Üst sınır, filtresiz bir aktarmanın
    // milyonlarca satır çekmesini engelliyor.
    public async Task<List<Contract>> GetContractsForExportAsync(
        int? createdByUserId,
        ContractStatus[]? includeStatuses,
        ContractStatus[]? excludeStatuses,
        string? searchText,
        string? type,
        int maxRows)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var query = BuildContractQuery(db, createdByUserId, includeStatuses, excludeStatuses, searchText, type);

        return await query
            .Include(c => c.CreatedByUser)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<List<AuditLog>> GetAuditLogsForExportAsync(
        string? userText, DateTime? startDate, DateTime? endDate, string? action, int maxRows)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var query = BuildAuditLogQuery(db, userText, startDate, endDate, action);

        return await query
            .OrderByDescending(a => a.ActionDate)
            .Take(maxRows)
            .ToListAsync();
    }

    // Filtre mantığı sayfalı ve dışa aktarma sorguları arasında paylaşılıyor;
    // iki yerde ayrı yazılsaydı ekranda görülenle aktarılan kayıtlar zamanla ayrışırdı.
    private static IQueryable<Contract> BuildContractQuery(
        SysDbContext db,
        int? createdByUserId,
        ContractStatus[]? includeStatuses,
        ContractStatus[]? excludeStatuses,
        string? searchText,
        string? type = null)
    {
        var query = db.Contracts.AsNoTracking().AsQueryable();

        if (createdByUserId.HasValue)
            query = query.Where(c => c.CreatedByUserId == createdByUserId.Value);

        if (includeStatuses is { Length: > 0 })
            query = query.Where(c => includeStatuses.Contains(c.Status));

        if (excludeStatuses is { Length: > 0 })
            query = query.Where(c => !excludeStatuses.Contains(c.Status));

        // Tür filtresi. Gösterge panelindeki tür dağılımından tıklanarak gelinir;
        // ekranda da bir açılır listeyle seçilebilir. Durum filtresinden BAĞIMSIZ
        // bir boyut: "Aktif + Hizmet" gibi birleşimler mümkün olmalı.
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(c => c.Type == type);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            // EF.Functions.Like ile SQL Server tarafında büyük/küçük harf duyarsız arama
            // (varsayılan collation case-insensitive olduğu için ek bir dönüşüm gerekmez).
            var pattern = $"%{searchText.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Title, pattern) ||
                EF.Functions.Like(c.CompanyName, pattern) ||
                (c.ContractNo != null && EF.Functions.Like(c.ContractNo, pattern)) ||
                EF.Functions.Like(c.RequestRefNo, pattern));
        }

        return query;
    }

    private static IQueryable<AuditLog> BuildAuditLogQuery(
        SysDbContext db, string? userText, DateTime? startDate, DateTime? endDate, string? action)
    {
        var query = db.AuditLogs.AsNoTracking().Include(a => a.ActingUser).AsQueryable();

        if (!string.IsNullOrEmpty(userText))
            query = query.Where(a => (a.ActingUser != null ? a.ActingUser.FullName : ("Kullanıcı #" + a.ActingUserId)) == userText);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        if (startDate.HasValue)
            query = query.Where(a => a.ActionDate.Date >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(a => a.ActionDate.Date <= endDate.Value.Date);

        return query;
    }

    // Filtre + arama + sayfalama tek bir SQL sorgusunda yapılır; sayfa dışındaki
    // kayıtlar hiç belleğe alınmaz. Toplam kayıt sayısı ayrı bir COUNT ile alınır
    // (AuditLog ekranındaki GetAuditLogsPagedAsync ile aynı desen).
    public async Task<(List<Contract> Items, int TotalCount)> GetContractsPagedAsync(
        int? createdByUserId,
        ContractStatus[]? includeStatuses,
        ContractStatus[]? excludeStatuses,
        string? searchText,
        string? type,
        int page,
        int pageSize)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        // Filtre mantığı dışa aktarma sorgusuyla paylaşılıyor.
        var query = BuildContractQuery(db, createdByUserId, includeStatuses, excludeStatuses, searchText, type);

        var totalCount = await query.CountAsync();

        // Sayfa numarası sınırların dışına taşarsa (örn. filtre daraldığında) son
        // geçerli sayfaya çekilir; aksi halde kullanıcı boş bir sayfada kalırdı.
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // --- Gösterge paneli toplamları ---

    // "Yürürlükteki" sayılan durumlar: panelin değer/dağılım kutuları bu kümeye bakar.
    private static readonly ContractStatus[] LiveStatuses =
        { ContractStatus.Aktif, ContractStatus.Uyari, ContractStatus.Ihlal };

    private static IQueryable<Contract> ScopeToUser(IQueryable<Contract> query, int? createdByUserId)
        => createdByUserId.HasValue ? query.Where(c => c.CreatedByUserId == createdByUserId.Value) : query;

    public async Task<List<CurrencyTotal>> GetActiveValueByCurrencyAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var query = ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .Where(c => LiveStatuses.Contains(c.Status));

        var rows = await query
            .GroupBy(c => c.Currency)
            .Select(g => new { Currency = g.Key, Amount = g.Sum(c => c.TotalAmount), Count = g.Count() })
            .ToListAsync();

        return rows
            .OrderByDescending(r => r.Amount)
            .Select(r => new CurrencyTotal(r.Currency, r.Amount, r.Count))
            .ToList();
    }

    public async Task<MonthlyStats> GetMonthlyStatsAsync(int? createdByUserId, DateTime monthStart, DateTime monthEnd)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var newRequests = await ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .CountAsync(c => c.CreatedAt >= monthStart && c.CreatedAt < monthEnd);

        // "Yürürlüğe giren" ve "feshedilen" bilgisi sözleşmede tarihli olarak tutulmuyor;
        // onay kayıtlarından çıkarılıyor. Son aşama (3) kararı bu ay verilmişse sayılır.
        var contractIds = ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId).Select(c => c.Id);

        var activated = await db.ApprovalLogs.AsNoTracking()
            .Where(a => contractIds.Contains(a.ContractId))
            .Where(a => a.StepNumber == 2 && a.Decision == ApprovalDecision.Onay)
            .CountAsync(a => a.ActionDate >= monthStart && a.ActionDate < monthEnd);

        var terminated = await ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .Where(c => c.Status == ContractStatus.Feshedildi)
            .Join(db.ContractTerminations.AsNoTracking(),
                  c => c.Id, t => t.ContractId, (c, t) => t.RequestedAt)
            .CountAsync(d => d >= monthStart && d < monthEnd);

        return new MonthlyStats
        {
            NewRequests = newRequests,
            Activated = activated,
            Terminated = terminated,
        };
    }

    public async Task<List<TypeCount>> GetTypeBreakdownAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var rows = await ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .Where(c => LiveStatuses.Contains(c.Status))
            .GroupBy(c => c.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows
            .OrderByDescending(r => r.Count)
            .Select(r => new TypeCount(string.IsNullOrWhiteSpace(r.Type) ? "Belirtilmemiş" : r.Type, r.Count))
            .ToList();
    }

    public async Task<int> CountByStageAsync(int stage)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts.AsNoTracking().CountAsync(c => c.Stage == stage);
    }

    public async Task<int> CountByStatusesAsync(int? createdByUserId, params ContractStatus[] statuses)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .CountAsync(c => statuses.Contains(c.Status));
    }

    public async Task<int> CountRejectedRequestsAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .CountAsync(c => c.Status == ContractStatus.Talep && c.WasRejected);
    }

    // Verilen sözleşmelerden hangileri için zaten bir yenileme talebi açılmış?
    //
    // "Yaklaşan Bitişler" listesi, o sözleşmenin yenilenip yenilenmediğini
    // söylemiyordu; SYB aynı sözleşmeyi her gün listede görüp "bunu yenilemiş
    // miydik" diye tek tek kontrol etmek zorundaydı.
    //
    // Fesih ve red edilmiş yenileme talepleri sayılmıyor: onlar yenileme borcunu
    // kapatmaz, sözleşmenin hâlâ yenilenmesi gerekir.
    public async Task<HashSet<int>> GetRenewedContractIdsAsync(IEnumerable<int> sourceContractIds)
    {
        var ids = sourceContractIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<int>();

        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var found = await db.Contracts
            .AsNoTracking()
            .Where(c => c.RenewedFromContractId != null
                        && ids.Contains(c.RenewedFromContractId.Value)
                        && c.Status != ContractStatus.Reddedildi
                        && c.Status != ContractStatus.Feshedildi)
            .Select(c => c.RenewedFromContractId!.Value)
            .Distinct()
            .ToListAsync();

        return found.ToHashSet();
    }

    // Belirli bir aşamada bekleyen en ESKİ kaydın oluşturulma tarihi.
    //
    // Onay kuyruğunda "12 iş bekliyor" yazıyor ama bunlardan birinin üç haftadır
    // beklediği hiçbir yerde görünmüyordu. Sözleşme onaylayan bir sistemde bekleme
    // süresi, adetten daha anlamlı bir metrik.
    public async Task<DateTime?> GetOldestPendingCreatedAtAsync(int stage)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Contracts
            .AsNoTracking()
            .Where(c => c.Stage == stage)
            .OrderBy(c => c.CreatedAt)
            .Select(c => (DateTime?)c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    // Açık (henüz giderilmemiş) ihlal SAYISI.
    //
    // Gösterge panelindeki ihlal kartı, durumu "Ihlal" olan SÖZLEŞME sayısını
    // gösteriyordu. İhlal giderme özelliği eklendikten sonra asıl anlamlı sayı bu:
    // bir sözleşmede üç açık ihlal olabilir ve kart "1" derken aslında üç iş bekliyor.
    //
    // ÖNEMLİ: Yalnızca durumu "Ihlal" olan sözleşmelerin ihlalleri sayılır.
    //
    // Sebep, kartın SAYISI ile TIKLANINCA GİDİLEN YER'in aynı kümeyi göstermesi.
    // Kart "ihlal" filtresine götürüyor, o filtre de Status == Ihlal olanları
    // listeliyor. Sayım daha geniş bir küme kullansaydı kullanıcı "3 açık ihlal"
    // görüp tıklıyor, listede iki sözleşme buluyor ve üçüncüyü arıyor olurdu.
    //
    // Kapsam dışında kalan iki durum ve neden sorun olmadıkları:
    //
    //   - Kapanmış sözleşme (Tamamlandı/Feshedildi): ihlal kaydı açık kalmış
    //     olabilir ama artık bir aksiyon gerektirmiyor.
    //   - Onay zincirindeki sözleşme (OnayBekliyor): düzenleme ya da fesih talebi
    //     karara bağlanana kadar durum geçici olarak OnayBekliyor'dur; ihlal
    //     "donmuş" sayılır. Talep reddedilirse sözleşme Ihlal'e geri döner ve
    //     ihlal yeniden sayıma girer.
    public async Task<int> CountOpenViolationsAsync(int? createdByUserId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        // Personel yalnızca kendi sözleşmelerindeki ihlalleri görür. Violation'da
        // kullanıcı alanı yok, bu yüzden sözleşme üzerinden daraltılıyor.
        var contracts = ScopeToUser(db.Contracts.AsNoTracking(), createdByUserId)
            .Where(c => c.Status == ContractStatus.Ihlal);

        return await db.Violations
            .AsNoTracking()
            .Where(v => v.ResolvedAt == null && contracts.Any(c => c.Id == v.ContractId))
            .CountAsync();
    }

    public async Task<int> ReconcileStatusesAsync(DateTime today, DateTime warningThreshold)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        // İhlal durumu da adaylara dahil: eskiden yalnızca Aktif/Uyarı taranıyordu,
        // dolayısıyla hakkında ihlal bildirilmiş bir sözleşme süresi dolsa bile
        // sonsuza kadar "İhlal Mevcut" olarak kalıyor ve arşive hiç düşmüyordu.
        var candidates = await db.Contracts
            .Where(c => c.Status == ContractStatus.Aktif
                     || c.Status == ContractStatus.Uyari
                     || c.Status == ContractStatus.Ihlal)
            .Where(c => c.EndDate != null)
            .ToListAsync();

        int updated = 0;
        foreach (var c in candidates)
        {
            ContractStatus newStatus;

            if (c.EndDate!.Value.Date < today)
            {
                // Süre dolduysa sözleşme her hâlükârda tamamlanmıştır — ihlal kaydı
                // geçmişte durmaya devam eder, sözleşmenin kendisi arşive gider.
                newStatus = ContractStatus.Tamamlandi;
            }
            else if (c.Status == ContractStatus.Ihlal)
            {
                // Süresi dolmamış ihlalli sözleşme Aktif/Uyarı'ya geri çekilmez;
                // ihlal bilgisi bakım işi tarafından silinmemeli.
                continue;
            }
            else
            {
                newStatus = c.EndDate.Value.Date <= warningThreshold
                    ? ContractStatus.Uyari
                    : ContractStatus.Aktif;
            }

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

    public async Task ResolveViolationAsync(Contract contract, Violation violation, AuditLog auditLog)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        var tracked = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        CopyWorkflowState(contract, tracked);
        db.Entry(tracked).Property(c => c.RowVersion).OriginalValue = contract.RowVersion;

        // Yalnızca çözüm alanları güncelleniyor; ihlalin kendisi (tür, tarih, açıklama)
        // değişmemeli.
        var trackedViolation = await db.Violations.FirstOrDefaultAsync(v => v.Id == violation.Id);
        if (trackedViolation is not null)
        {
            trackedViolation.ResolvedAt = violation.ResolvedAt;
            trackedViolation.ResolvedByUserId = violation.ResolvedByUserId;
            trackedViolation.ResolutionNote = violation.ResolutionNote;
        }

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

    public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
        int page, int pageSize, string? userText, DateTime? startDate, DateTime? endDate, string? action = null)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);

        // Filtre mantığı dışa aktarma sorgusuyla paylaşılıyor. İşlem türü filtresi,
        // denetimde en sık sorulan "tüm şifre sıfırlamaları" gibi soruları karşılıyor.
        var query = BuildAuditLogQuery(db, userText, startDate, endDate, action);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.ActionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}