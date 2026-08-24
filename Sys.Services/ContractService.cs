using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sys.Domain;
namespace Sys.Services;

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
    public async Task<List<Contract>> GetContractsAsync(User currentUser)
    {
        return currentUser.Role == UserRole.Personel
            ? await _contracts.GetByCreatedUserAsync(currentUser.Id)
            : await _contracts.GetAllAsync();
    }

    // Gösterge panelinin tamamını tek çağrıda doldurur. Kutu başına ayrı servis
    // çağrısı yapmak hem yavaş olur hem de ekran parça parça dolardı.
    public async Task<DashboardSummary> GetDashboardSummaryAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var countsTask = _contracts.GetStatusCountsAsync(userId);
        var valueTask = _contracts.GetActiveValueByCurrencyAsync(userId);
        var monthlyTask = _contracts.GetMonthlyStatsAsync(userId, monthStart, monthEnd);
        var endingsTask = _contracts.GetEndingCalendarAsync(userId, today);
        var typesTask = _contracts.GetTypeBreakdownAsync(userId);
        var upcomingTask = GetUpcomingEndingsAsync(currentUser);
        var activityTask = GetRecentActivityAsync(currentUser);
        var pendingTask = BuildPendingWorkAsync(currentUser, userId);

        await Task.WhenAll(countsTask, valueTask, monthlyTask, endingsTask,
                           typesTask, upcomingTask, activityTask, pendingTask);

        var counts = countsTask.Result;

        return new DashboardSummary
        {
            Aktif = counts.GetValueOrDefault(ContractStatus.Aktif),
            OnayBekliyor = counts.GetValueOrDefault(ContractStatus.OnayBekliyor),
            Uyari = counts.GetValueOrDefault(ContractStatus.Uyari),
            Ihlal = counts.GetValueOrDefault(ContractStatus.Ihlal),

            ActiveValue = valueTask.Result,
            ThisMonth = monthlyTask.Result,
            Endings = endingsTask.Result,
            TypeBreakdown = typesTask.Result,
            UpcomingEndings = upcomingTask.Result,
            RecentActivity = activityTask.Result,
            PendingWork = pendingTask.Result,
        };
    }

    // "Sizi bekleyen işler": kullanıcının KENDİ aksiyonunu bekleyen işler.
    // Panelin en önemli kutusu — kullanıcı "onay bekliyor: 7" gördüğünde bunun
    // kaçının kendisini beklediğini bilmiyordu.
    private async Task<List<PendingWorkItem>> BuildPendingWorkAsync(User currentUser, int? userId)
    {
        var items = new List<PendingWorkItem>();

        switch (currentUser.Role)
        {
            case UserRole.SYB:
                var yaratilacak = await _contracts.CountByStatusesAsync(null, ContractStatus.Talep);
                if (yaratilacak > 0)
                    items.Add(new PendingWorkItem(
                        "Sözleşmeye dönüştürülecek talepler",
                        "Onaylanmış talepler sözleşme oluşturulmayı bekliyor",
                        yaratilacak, "sozlesmeYarat", "#7C3AED"));

                var sonKontrol = await _contracts.CountByStageAsync(1);
                if (sonKontrol > 0)
                    items.Add(new PendingWorkItem(
                        "Son kontrolünüzde bekleyenler",
                        "SYB onayı verilmemiş sözleşmeler",
                        sonKontrol, "sozlesmeKontrol", "#B06A00"));
                break;

            case UserRole.Mudur:
                var mudurOnay = await _contracts.CountByStageAsync(2);
                if (mudurOnay > 0)
                    items.Add(new PendingWorkItem(
                        "Onayınızda bekleyenler",
                        "Yönetim onayı verilmemiş sözleşmeler",
                        mudurOnay, "onayBekleyen", "#2D6EA8"));
                break;

            case UserRole.Personel:
                var reddedilen = await _contracts.CountRejectedRequestsAsync(userId);
                if (reddedilen > 0)
                    items.Add(new PendingWorkItem(
                        "Düzeltme bekleyen talepleriniz",
                        "Reddedilmiş, yeniden gönderilmesi gereken talepler",
                        reddedilen, "talepList", "#A32D2D"));
                break;
        }

        return items;
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

    // Excel'e aktarmada tek seferde çekilecek azami satır sayısı. Filtresiz bir
    // aktarmanın tüm tabloyu belleğe almasını engelliyor; sınıra takıldığında
    // kullanıcı uyarılıp filtreyi daraltması isteniyor.
    public const int MaxExportRows = 10_000;

    // Ekrandaki filtrelerin aynısıyla, ama sayfalamadan. Dışa aktarmanın amacı tüm
    // eşleşen kayıtları analiz edebilmek; yalnızca görünen sayfayı aktarmak işe yaramaz.
    public async Task<List<Contract>> GetContractsForExportAsync(User currentUser, string filterKey, string? searchText)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var (include, exclude) = MapFilter(filterKey);
        return await _contracts.GetContractsForExportAsync(userId, include, exclude, searchText, MaxExportRows);
    }

    public async Task<List<Contract>> GetArchivedContractsForExportAsync(User currentUser, string filterKey, string? searchText)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetContractsForExportAsync(userId, MapArchiveFilter(filterKey), null, searchText, MaxExportRows);
    }

    public async Task<List<AuditLog>> GetAuditLogsForExportAsync(
        User currentUser, string? userText, DateTime? startDate, DateTime? endDate, string? action)
    {
        if (currentUser.Role != UserRole.Mudur)
            throw new InvalidOperationException("Bu işlem geçmişini görüntüleme yetkiniz yok.");

        return await _contracts.GetAuditLogsForExportAsync(userText, startDate, endDate, action, MaxExportRows);
    }

    // Dışa aktarma, sözleşme verisinin uygulama dışına çıkması demek — PDF ve yazdırma
    // gibi denetim kaydına yazılıyor. Kaç kaydın alındığı da kayda giriyor.
    public Task LogExportAsync(User actingUser, string what, int rowCount)
        => LogAuditAsync(0, "ListeDışaAktarıldı", actingUser.Id, $"{what} — {rowCount} kayıt Excel'e aktarıldı.");

    // "Tümü" seçildiğinde kapanmış sözleşmeler (Tamamlandı/Feshedildi) listede gösterilmez —
    // bu ekran devam eden işleri gösterir. Eskiden bu ayıklama ViewModel'de bellekte yapılıyordu.
    private static (ContractStatus[]? Include, ContractStatus[]? Exclude) MapFilter(string filterKey) => filterKey switch
    {
        "aktif" => (new[] { ContractStatus.Aktif }, null),
        "onay_bekliyor" => (new[] { ContractStatus.OnayBekliyor }, null),
        "uyari" => (new[] { ContractStatus.Uyari }, null),
        "ihlal" => (new[] { ContractStatus.Ihlal }, null),
        "tamamlandi" => (new[] { ContractStatus.Tamamlandi }, null),

        // Kapatılmış talepler "Tümü" listesinde görünmez (devam eden bir iş değiller);
        // kendi filtreleriyle burada, tam künyeleriyle de Arşiv ekranında bulunurlar.
        "reddedildi" => (new[] { ContractStatus.Reddedildi }, null),

        // "Tümü" = devam eden işler. Hariç tutulan küme Arşiv'in kapsadığı kümeyle
        // aynı olmalı; tek yerden (ArchivedStatuses) beslenerek ikisi senkron tutuluyor.
        _ => (null, ArchivedStatuses)
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
    // Talep oluşturma/güncelleme yalnızca Personel ve SYB'ye açıktır. Müdür yalnızca
    // onaylar, Admin ise sözleşme iş akışına hiç katılmaz.
    private static void EnsureCanCreateRequest(User actingUser)
    {
        if (actingUser.Role is not (UserRole.Personel or UserRole.SYB))
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");
    }

    // "Yürürlükte" sayılan durumlar: sözleşme onay zincirini tamamlamış ve henüz
    // kapanmamış. Düzenleme, fesih ve ihlal işlemleri yalnızca bu kümede anlamlıdır.
    private static readonly ContractStatus[] LiveStatuses =
    {
        ContractStatus.Aktif,
        ContractStatus.Uyari,
        ContractStatus.Ihlal
    };

    // Bu kontrol eskiden HİÇ yoktu; yalnızca rol bakılıyordu. Sonucu gerçek veride
    // görüldü: onay zincirinin ortasındaki (Status = OnayBekliyor, Stage = 2) bir
    // sözleşme düzenlemeye açıldı, düzenleme reddedilince kod sözleşmenin yürürlükte
    // olduğunu varsayıp Stage = 3 atadı ve kayıt "Onay Bekliyor / Stage 3" gibi
    // hiçbir onay kuyruğunda görünmeyen bir durumda kalıcı olarak takıldı.
    //
    // Zaten devam eden bir düzenleme/fesih varken ikincisinin başlatılması da burada
    // engelleniyor: PreviousStatusBeforeEdit/BeforeTermination tek değer tuttuğu için
    // ikinci istek birincinin geri dönüş noktasını eziyordu.
    private static void EnsureContractIsLive(Contract contract, string islem)
    {
        if (!LiveStatuses.Contains(contract.Status))
            throw new InvalidOperationException(
                $"Bu sözleşme için {islem} işlemi yapılamaz: sözleşme yürürlükte değil.");

        if (contract.PendingEdit)
            throw new InvalidOperationException(
                "Bu sözleşmede onay bekleyen bir düzenleme talebi var; sonuçlanmadan yeni işlem yapılamaz.");

        if (contract.PendingTermination)
            throw new InvalidOperationException(
                "Bu sözleşmede onay bekleyen bir fesih talebi var; sonuçlanmadan yeni işlem yapılamaz.");
    }

    // Yenilenebilecek sözleşmeler: süresi dolmuş ya da dolmak üzere olanlar.
    // Feshedilmiş bir sözleşme yenilenmez — taraflar ilişkiyi zaten sonlandırdı.
    private static readonly ContractStatus[] RenewableStatuses =
    {
        ContractStatus.Aktif,
        ContractStatus.Uyari,
        ContractStatus.Ihlal,
        ContractStatus.Tamamlandi
    };

    public static bool CanRenew(Contract contract, User actingUser)
        => actingUser.Role is UserRole.Personel or UserRole.SYB
        && RenewableStatuses.Contains(contract.Status);

    // Bir sözleşmenin hangi sözleşmenin yenilemesi olduğunu göstermek için.
    // Kaynak kayıt silinmiş ya da erişilemiyorsa null döner; bu durumda ekran
    // yenileme bilgisini hiç göstermez — yanlış bilgi göstermektense hiç göstermemek.
    public async Task<(string RefNo, DateTime? EndDate)?> GetRenewalSourceAsync(int sourceContractId)
        => await _contracts.GetRenewalSourceSummaryAsync(sourceContractId);

    public async Task<Contract> CreateRequestAsync(Contract contract, User actingUser)
    {
        EnsureCanCreateRequest(actingUser);

        // Sahiplik ve başlangıç durumu çağıranın gönderdiği değere bırakılmaz; servis
        // içinde sabitlenir. Aksi halde bir çağrı talebi başka bir kullanıcının üzerine
        // yazabilir ya da talebi doğrudan ileri bir aşamada başlatabilirdi.
        contract.Id = 0;
        contract.CreatedByUserId = actingUser.Id;
        contract.Status = ContractStatus.Talep;
        contract.Stage = 0;
        contract.CreatedAt = DateTime.Now;
        contract.WasRejected = false;
        contract.LastRejectionNote = null;
        contract.LastRejectedAt = null;
        // RenewedFromContractId'ye dokunulmuyor: yenileme bağı çağırandan geliyor ve
        // kaydın parçası olarak yazılıyor.
        await _contracts.AddAsync(contract);

        var detay = contract.RenewedFromContractId is null
            ? $"{contract.Title} için yeni talep oluşturuldu."
            : $"{contract.Title} için yenileme talebi oluşturuldu (kaynak sözleşme #{contract.RenewedFromContractId}).";

        await LogAuditAsync(contract.Id, "TalepOluşturuldu", contract.CreatedByUserId, detay);

        // Yeni talep henüz onay aşamasında değil (Stage 0), ama sözleşmeyi oluşturacak
        // olan SYB'nin talepten haberi olmalı — aksi halde listeyi elle taramak gerekir.
        var sybIds = await GetActiveUserIdsByRoleAsync(UserRole.SYB);
        await NotifyAsync(sybIds, contract.Id, NotificationType.SozlesmeOlayi,
            "Yeni sözleşme talebi", $"\"{contract.Title}\" için yeni bir sözleşme talebi oluşturuldu.");

        return contract;
    }
    public async Task UpdateRequestAsync(Contract contract, User actingUser)
    {
        EnsureCanCreateRequest(actingUser);

        // GetContractDetailAsync, Personel için başkasının talebinde null döner —
        // sahiplik kontrolü buradan gelir. Rol kontrolü ise yukarıda ayrıca yapılır,
        // böylece Müdür/Admin talep düzenleyemez.
        var existing = await GetContractDetailAsync(contract.Id, actingUser);
        if (existing is null)
            throw new InvalidOperationException("Bu talebi düzenleme yetkiniz yok.");
        // Reddedilip kapatılan talep, iade edilenden farklı olarak yeniden gönderilemez —
        // kullanıcının nedenini anlaması için ayrı bir mesaj veriliyor.
        if (existing.Status == ContractStatus.Reddedildi)
            throw new InvalidOperationException("Bu talep reddedilerek kapatılmıştır, yeniden gönderilemez. Gerekiyorsa yeni bir talep oluşturun.");
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
    // Henüz sözleşmeye dönüşmemiş bir talebin (Status = Talep, Stage = 0) SYB tarafından
    // reddedilmesi. DecideApprovalAsync yalnızca Stage 1-2 için çalıştığı için bu aşamada
    // hiçbir red yolu yoktu: SYB'nin geçersiz bir talebi reddedebilmesi için önce kalemleri
    // ve tarihleri girip sözleşmeyi YARATMASI, ardından Son Kontrol'de kendi yarattığı
    // kaydı reddetmesi gerekiyordu — bu sırada boşuna bir sözleşme numarası da yakılıyordu.
    //
    // allowResubmit:
    //   true  → İade. Talep sahibine geri döner, düzeltip yeniden gönderebilir.
    //           Durum Talep olarak kalır, mevcut WasRejected mekanizması kullanılır.
    //   false → Kapatma. Durum Reddedildi olur; UpdateRequestAsync artık bu talebi
    //           düzenlemeye izin vermez, süreç nihai olarak biter.
    public async Task RejectRequestAsync(Contract contract, User actingUser, string? note, bool allowResubmit)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        // Sözleşmeye dönüşmüş kayıtlar bu yoldan reddedilemez; onların yeri onay
        // zinciridir (DecideApprovalAsync). Aksi halde onay aşamasındaki bir sözleşme
        // zincir atlanarak kapatılabilirdi.
        if (contract.Status != ContractStatus.Talep)
            throw new InvalidOperationException("Yalnızca henüz sözleşmeye dönüşmemiş talepler reddedilebilir.");

        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("Reddetme işlemi için bir gerekçe girilmelidir.");

        contract.Stage = 0;
        contract.Status = allowResubmit ? ContractStatus.Talep : ContractStatus.Reddedildi;
        contract.WasRejected = true;
        contract.LastRejectionNote = note;
        contract.LastRejectedAt = DateTime.Now;

        // Karar, sözleşme detayındaki zaman çizelgesinde de görünsün diye ApprovalLog
        // olarak yazılır. StepNumber 0 = onay zinciri öncesi talep incelemesi.
        var log = new ApprovalLog
        {
            StepNumber = 0,
            StepName = allowResubmit ? "Talep İncelemesi (İade)" : "Talep İncelemesi (Kapatıldı)",
            ActingUserId = actingUser.Id,
            Decision = ApprovalDecision.Red,
            Note = note,
            ActionDate = DateTime.Now
        };

        var auditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = allowResubmit ? "TalepİadeEdildi" : "TalepReddedildi",
            ActingUserId = actingUser.Id,
            Detail = $"{contract.Title} talebi " + (allowResubmit ? "düzeltilmek üzere iade edildi" : "reddedilip kapatıldı") + $" - Not: {note}",
            ActionDate = DateTime.Now,
        };

        await _contracts.ApplyDecisionAsync(contract, log, auditLog);

        var (baslik, mesaj) = allowResubmit
            ? ("Talebiniz iade edildi",
               $"\"{contract.Title}\" talebi düzeltilmek üzere iade edildi. Gerekçe: {note}")
            : ("Talebiniz reddedildi",
               $"\"{contract.Title}\" talebi reddedildi ve kapatıldı. Gerekçe: {note}");

        await NotifyAsync(new[] { contract.CreatedByUserId }, contract.Id,
            NotificationType.TalepSonucu, baslik, mesaj);
    }

    public async Task AddAttachmentAsync(Attachment attachment, User actingUser)
    {
        if (actingUser.Role == UserRole.Admin)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        // Kullanıcının bu sözleşmeye erişimi var mı? GetContractDetailAsync, Personel
        // için başkasının sözleşmesinde null döner — böylece bir kullanıcı görmediği
        // bir sözleşmeye dosya ekleyemez.
        var contract = await GetContractDetailAsync(attachment.ContractId, actingUser);
        if (contract is null)
            throw new InvalidOperationException("Bu sözleşmeye dosya ekleme yetkiniz yok.");

        // Yükleyen bilgisi çağıranın gönderdiği değere bırakılmaz.
        attachment.UploadedByUserId = actingUser.Id;
        if (attachment.UploadedAt == default)
            attachment.UploadedAt = DateTime.Now;

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

        // Yalnızca bekleyen bir talep sözleşmeye dönüştürülebilir. Bu kontrol olmadan
        // reddedilip kapatılmış bir talep (ya da hâlihazırda yürürlükteki bir sözleşme)
        // yeniden onay zincirinin başına gönderilebilirdi.
        if (contract.Status != ContractStatus.Talep)
            throw new InvalidOperationException("Bu talep sözleşmeye dönüştürülemez; artık bekleyen bir talep değil.");

        // Tarih doğrulaması sadece sihirbaz ekranında vardı; servis hiç bakmıyordu.
        // Tarihsiz bir sözleşme "Aktif" olabiliyordu — o durumda kalan gün
        // hesaplanamıyor, bakım işi süresi dolmuşları hiç göremiyor ve sözleşme
        // sonsuza kadar yürürlükte kalıyordu.
        if (contract.StartDate is null || contract.EndDate is null)
            throw new InvalidOperationException("Sözleşmenin başlangıç ve bitiş tarihi girilmelidir.");

        if (contract.EndDate.Value.Date <= contract.StartDate.Value.Date)
            throw new InvalidOperationException("Bitiş tarihi, başlangıç tarihinden sonra olmalıdır.");

        if (items.Count == 0)
            throw new InvalidOperationException("Sözleşmede en az bir bedel kalemi bulunmalıdır.");

        contract.TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice);
        contract.Status = ContractStatus.OnayBekliyor;
        contract.Stage = 1;

        // Talep aşamasındaki red işareti burada temizlenir: talep artık sözleşmeye
        // dönüştü. Aksi halde talep reddinin gerekçesi, Son Kontrol'deki yeni
        // sözleşmenin altında alakasız bir uyarı olarak görünmeye devam ederdi.
        // (Red kaydının kendisi ApprovalLog'da kalır, detaydaki zaman çizelgesinde görünür.)
        contract.WasRejected = false;
        contract.LastRejectionNote = null;
        contract.LastRejectedAt = null;
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
    // Karar bekleyen (sonucu henüz yazılmamış) düzenleme kaydı. Eski kayıtlarda
    // IsApproved null olduğu için, en yenisinden başlanarak aranıyor.
    private static ContractRevision? FindPendingRevision(Contract contract)
        => contract.Revisions
            .Where(r => r.IsApproved is null)
            .OrderByDescending(r => r.ChangedAt)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    private static ContractTermination? FindPendingTermination(Contract contract)
        => contract.Terminations
            .Where(t => t.IsApproved is null)
            .OrderByDescending(t => t.RequestedAt)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault();

    private static ContractRevision? MarkPendingRevision(Contract contract, bool approved)
    {
        var revision = FindPendingRevision(contract);
        if (revision is null) return null;

        revision.IsApproved = approved;
        revision.ResolvedAt = DateTime.Now;
        return revision;
    }

    private static ContractTermination? MarkPendingTermination(Contract contract, bool approved)
    {
        var termination = FindPendingTermination(contract);
        if (termination is null) return null;

        termination.IsApproved = approved;
        termination.ResolvedAt = DateTime.Now;
        return termination;
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
        // Karara bağlanan düzenleme/fesih kaydı. Sonuç yalnızca zincir BİTTİĞİNDE
        // (onay zinciri tamamlandığında ya da red geldiğinde) işaretlenir; Stage 1
        // onayı talebi bir sonraki aşamaya taşır, henüz sonuçlandırmaz.
        ContractRevision? resolvedRevision = null;
        ContractTermination? resolvedTermination = null;

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
                    resolvedTermination = MarkPendingTermination(contract, approved: true);
                }
            }
            else
            {
                // Fesih talebi reddedildi — sözleşme fesih öncesi durumuna (Aktif veya Uyarı) döner
                contract.Stage = 3;
                contract.Status = contract.PreviousStatusBeforeTermination ?? ContractStatus.Aktif;
                contract.PendingTermination = false;
                contract.PreviousStatusBeforeTermination = null;
                resolvedTermination = MarkPendingTermination(contract, approved: false);
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
                    resolvedRevision = MarkPendingRevision(contract, approved: true);
                }
            }
            else
            {
                // Düzenleme talebi reddedildi — sözleşme düzenleme öncesi durumuna döner, Talep'e düşmez.
                // Bedel ve bitiş tarihi de bekleyen revizyondaki eski değerlere geri alınır; aksi
                // halde onaylanmamış değişiklik sözleşmede kalıcı olarak kalırdı.
                //
                // Geri alma artık "en son revizyon" yerine BEKLEYEN revizyon üzerinden yapılıyor:
                // sonuç alanları eklendiği için hangi kaydın karara bağlandığı kesin olarak biliniyor.
                var pending = FindPendingRevision(contract);
                if (pending is not null)
                {
                    contract.TotalAmount = pending.PreviousTotalAmount;
                    contract.EndDate = pending.PreviousEndDate;
                    contract.Description = pending.PreviousDescription;
                    contract.CompanyName = pending.PreviousCompanyName;
                    contract.TaxNo = pending.PreviousTaxNo;
                    contract.PaymentPeriod = pending.PreviousPaymentPeriod;
                }
                contract.Stage = 3;
                contract.Status = contract.PreviousStatusBeforeEdit ?? ContractStatus.Aktif;
                contract.PendingEdit = false;
                contract.PreviousStatusBeforeEdit = null;
                resolvedRevision = MarkPendingRevision(contract, approved: false);
            }
        }
        else
        {
            if (decision == ApprovalDecision.Onay)
            {
                // Zincirde ileri gidildiği an "reddedilmişti" işareti temizlenir; aksi
                // halde red notu sözleşme yürürlüğe girdikten sonra da ekranda kalırdı.
                contract.WasRejected = false;
                contract.LastRejectionNote = null;
                contract.LastRejectedAt = null;

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
                // Her iki red yönü de aynı alanlara yazılır. Eskiden yalnızca Stage 1
                // reddi (talebe geri dönüş) işaretleniyordu; Müdür Stage 2'de reddedip
                // sözleşmeyi SYB'ye geri gönderdiğinde hiçbir iz kalmıyordu. SYB kaydı
                // onay kuyruğunda sanki ilk kez inceliyormuş gibi görüyor, red gerekçesini
                // ancak detay ekranındaki zaman çizelgesinden bulabiliyordu.
                contract.WasRejected = true;
                contract.LastRejectionNote = note;
                contract.LastRejectedAt = DateTime.Now;

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
        await _contracts.ApplyDecisionAsync(contract, log, auditLog, resolvedRevision, resolvedTermination);

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

    // Onay kuyruğunun sayfalanmış hâli. Diğer listeler sayfalanırken bu ekran tüm
    // bekleyen kayıtları tek seferde çekmeye devam ediyordu.
    public async Task<(List<Contract> Items, int TotalCount)> GetPendingApprovalsPagedAsync(
        User currentUser, int page, int pageSize)
    {
        int stage = currentUser.Role switch
        {
            UserRole.SYB => 1,
            UserRole.Mudur => 2,
            _ => -1
        };
        if (stage == -1) return (new List<Contract>(), 0);
        return await _contracts.GetByStagePagedAsync(stage, page, pageSize);
    }
    // Düzenleme / fesih / ihlal ekranlarının açılır listeleri. Üçü de EnsureContractIsLive
    // ile AYNI kümeye (LiveStatuses) bağlı.
    //
    // Eskiden üçü ayrı ayrı yazılmıştı ve kümeler birbirini tutmuyordu: fesih listesi
    // İhlal durumunu atlıyordu, yani ihlal bildirilen bir sözleşme feshedilemiyordu —
    // oysa "Haklı Fesih (İhlal Nedeniyle)" diye bir fesih türü var. İhlal listesi de
    // Uyarı durumunu atlıyordu. Tek kaynağa bağlanınca bu sapma bir daha oluşamaz.
    public async Task<List<Contract>> GetEditableContractsAsync(User currentUser)
        => await GetLiveContractsAsync(currentUser);

    private async Task<List<Contract>> GetLiveContractsAsync(User currentUser)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        return await _contracts.GetByStatusesAsync(userId, LiveStatuses);
    }
    public async Task EditContractAsync(
        Contract contract, User actingUser, string changeType, string reason,
        decimal? newTotalAmount, DateTime? newEndDate,
        string? newDescription = null, string? newCompanyName = null, string? newTaxNo = null, string? newPaymentPeriod = null)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        EnsureContractIsLive(contract, "düzenleme");

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
    // Uyarı durumu da eklendi: EnsureContractIsLive Aktif/Uyarı/İhlal üçlüsüne izin
    // veriyor ama bu liste yalnızca Aktif ve İhlal döndürüyordu. Sonuç: bitişine 30
    // günden az kalmış (Uyarı) bir sözleşme için ihlal bildirilemiyordu — oysa ihlal
    // en çok sözleşmenin son döneminde ortaya çıkar.
    public async Task<List<Contract>> GetViolationReportableContractsAsync(User currentUser)
        => await GetLiveContractsAsync(currentUser);
    public async Task ReportViolationAsync(Contract contract, User reporter, string violationType, DateTime violationDate, string description)
    {
        if (reporter.Role == UserRole.Mudur)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        // Kontrol olmadan, süresi dolmuş ya da feshedilmiş bir sözleşmeye ihlal
        // bildirildiğinde Status = Ihlal atanıyor ve arşivdeki kayıt yeniden
        // yürürlükteymiş gibi listeye geri dönüyordu.
        EnsureContractIsLive(contract, "ihlal bildirimi");

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
    // Bir ihlalin giderildiğini kaydeder. Sözleşmenin başka açık ihlali kalmamışsa
    // durumu normale döner.
    //
    // Onay zinciri YOK — ihlal bildiriminin de yok, simetrik olsun diye. İhlali
    // kapatmak sözleşmeyi yöneten SYB'nin işi.
    public async Task ResolveViolationAsync(Contract contract, Violation violation, User actingUser, string? note)
    {
        if (actingUser.Role != UserRole.SYB)
            throw new InvalidOperationException("Bu işlemi yapma yetkiniz yok.");

        if (violation.IsResolved)
            throw new InvalidOperationException("Bu ihlal zaten giderildi olarak işaretlenmiş.");

        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("İhlalin nasıl giderildiği yazılmalıdır.");

        violation.ResolvedAt = DateTime.Now;
        violation.ResolvedByUserId = actingUser.Id;
        violation.ResolutionNote = note;

        // Sözleşme yalnızca TÜM açık ihlalleri kapandığında normale döner; birden fazla
        // ihlal bildirilmiş olabilir.
        var kalanAcikIhlal = contract.Violations.Any(v => v.Id != violation.Id && !v.IsResolved);

        var statusChanged = false;
        if (!kalanAcikIhlal && contract.Status == ContractStatus.Ihlal)
        {
            // Yeni durum bakım işiyle aynı kurala göre belirlenir (tek yerden):
            // süresi dolmuşsa Tamamlandı, bitişi yakınsa Uyarı, değilse Aktif.
            contract.Status = ResolveLiveStatus(contract.EndDate);
            statusChanged = true;
        }

        var auditLog = new AuditLog
        {
            EntityName = "Contract",
            EntityId = contract.Id,
            Action = "İhlalGiderildi",
            ActingUserId = actingUser.Id,
            Detail = $"{contract.Title} - {violation.ViolationType}: {note}",
            ActionDate = DateTime.Now,
        };

        await _contracts.ResolveViolationAsync(contract, violation, auditLog);

        var hedefler = await GetActiveUserIdsByRoleAsync(UserRole.SYB);
        hedefler.Add(contract.CreatedByUserId);

        var mesaj = statusChanged
            ? $"\"{contract.Title}\" sözleşmesindeki ihlal giderildi; sözleşme yeniden {ContractStatusText(contract.Status)} durumuna döndü."
            : $"\"{contract.Title}\" sözleşmesinde bir ihlal giderildi. Sözleşmede hâlâ açık ihlal var.";

        await NotifyAsync(hedefler, contract.Id, NotificationType.SozlesmeOlayi, "İhlal giderildi", mesaj);
    }

    // Yürürlükteki bir sözleşmenin bitiş tarihine göre alacağı durum.
    // ReconcileStatusesAsync ile aynı eşikler kullanılıyor.
    private static ContractStatus ResolveLiveStatus(DateTime? endDate)
    {
        if (endDate is null) return ContractStatus.Aktif;

        var today = DateTime.Today;
        if (endDate.Value.Date < today) return ContractStatus.Tamamlandi;
        return endDate.Value.Date <= today.AddDays(30) ? ContractStatus.Uyari : ContractStatus.Aktif;
    }

    private static string ContractStatusText(ContractStatus status) => status switch
    {
        ContractStatus.Aktif => "Aktif",
        ContractStatus.Uyari => "Bitiş Yaklaşıyor",
        ContractStatus.Tamamlandi => "Tamamlandı",
        _ => status.ToString()
    };

    public async Task<List<Contract>> GetTerminableContractsAsync(User currentUser)
        => await GetLiveContractsAsync(currentUser);
    // Arşiv, sözleşmenin/talebin ARTIK İŞLEM GÖRMEYECEĞİ tüm son durumları kapsar:
    // süresi dolanlar, feshedilenler ve sözleşmeye hiç dönüşmeden kapatılan talepler.
    // Reddedilen talepler eskiden hiçbir listede görünmüyordu — ne aktif listede
    // (kapanmış oldukları için) ne de arşivde (kapsam dışı oldukları için).
    public static readonly ContractStatus[] ArchivedStatuses =
    {
        ContractStatus.Tamamlandi,
        ContractStatus.Feshedildi,
        ContractStatus.Reddedildi
    };

    // Arşiv zamanla sürekli büyüyen bir liste; tamamını belleğe çekmek yerine
    // sözleşme listesiyle aynı sayfalama/arama altyapısı kullanılıyor.
    public async Task<(List<Contract> Items, int TotalCount)> GetArchivedContractsPagedAsync(
        User currentUser, string filterKey, string? searchText, int page, int pageSize)
    {
        int? userId = currentUser.Role == UserRole.Personel ? currentUser.Id : null;
        var include = MapArchiveFilter(filterKey);
        return await _contracts.GetContractsPagedAsync(userId, include, null, searchText, page, pageSize);
    }

    private static ContractStatus[] MapArchiveFilter(string filterKey) => filterKey switch
    {
        "tamamlandi" => new[] { ContractStatus.Tamamlandi },
        "feshedildi" => new[] { ContractStatus.Feshedildi },
        "reddedildi" => new[] { ContractStatus.Reddedildi },
        _ => ArchivedStatuses
    };
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

        EnsureContractIsLive(contract, "fesih");

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

    public async Task<(List<AuditLog> Items, int TotalCount)> GetAuditLogsAsync(
        User currentUser, int page, int pageSize, string? userText,
        DateTime? startDate, DateTime? endDate, string? action = null)
    {
        if (currentUser.Role != UserRole.Mudur)
            throw new InvalidOperationException("Bu işlem geçmişini görüntüleme yetkiniz yok.");
        return await _contracts.GetAuditLogsPagedAsync(page, pageSize, userText, startDate, endDate, action);
    }
}