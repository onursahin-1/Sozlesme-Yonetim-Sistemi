using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Yaklaşan bitiş bildirimleri. Bu tarama saat başı çalışıyor; eşik seçimi ya da
// tekrar engeli bozulursa kullanıcılar ya hiç uyarı almaz ya da her saat aynı
// uyarıyı alır. İkisi de sessiz hatalar — bu yüzden test ediliyor.
public class NotificationServiceTests
{
    private const int OwnerId = 5;
    private const int SybId = 3;

    private static (NotificationService Service, FakeContractRepository Contracts,
                    FakeNotificationRepository Notifications) Create(params Contract[] contracts)
    {
        var contractRepo = new FakeContractRepository { StatusQueryResult = contracts.ToList() };

        var userRepo = new FakeUserRepository();
        userRepo.Users.Add(new User { Id = OwnerId, Username = "personel", Role = UserRole.Personel });
        userRepo.Users.Add(new User { Id = SybId, Username = "syb", Role = UserRole.SYB });

        var notifications = new FakeNotificationRepository();

        return (new NotificationService(notifications, contractRepo, userRepo), contractRepo, notifications);
    }

    private static Contract Ending(int daysLeft, int id = 1) => new()
    {
        Id = id,
        Title = "Temizlik Hizmeti",
        Status = ContractStatus.Aktif,
        EndDate = DateTime.Today.AddDays(daysLeft),
        CreatedByUserId = OwnerId,
    };

    // ---- Eşik seçimi ----

    // Geçilen EN KÜÇÜK eşik seçilir: bitişe 10 gün kalmışsa 15'lik uyarı
    // üretilir, 30'luk zaten daha önce üretilmiştir.
    [Theory]
    [InlineData(30, 30)]
    [InlineData(25, 30)]
    [InlineData(15, 15)]
    [InlineData(10, 15)]
    [InlineData(7, 7)]
    [InlineData(0, 7)]
    public async Task Generate_PicksSmallestPassedThreshold(int daysLeft, int expectedThreshold)
    {
        var (service, _, notifications) = Create(Ending(daysLeft));

        await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.All(notifications.Added, n => Assert.EndsWith($":{expectedThreshold}", n.DedupeKey));
    }

    // Hiçbir eşiğe girmeyen sözleşme için bildirim üretilmez.
    [Fact]
    public async Task Generate_FarFromEnd_ProducesNothing()
    {
        var (service, _, notifications) = Create(Ending(60));

        await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.Empty(notifications.Added);
    }

    // Süresi geçmiş sözleşme atlanır; "eksi 5 gün kaldı" gibi bir uyarı anlamsız
    // olurdu. Bu kayıtları bakım işi Tamamlandi'ya çeker.
    [Fact]
    public async Task Generate_AlreadyExpired_ProducesNothing()
    {
        var (service, _, notifications) = Create(Ending(-5));

        await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.Empty(notifications.Added);
    }

    [Fact]
    public async Task Generate_NoEndDate_ProducesNothing()
    {
        var contract = Ending(10);
        contract.EndDate = null;
        var (service, _, notifications) = Create(contract);

        await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.Empty(notifications.Added);
    }

    // ---- Hedef kullanıcılar ----

    // Bildirim hem sözleşmeyi oluşturana hem de tüm aktif SYB kullanıcılarına gider.
    [Fact]
    public async Task Generate_NotifiesOwnerAndSyb()
    {
        var (service, _, notifications) = Create(Ending(10));

        await service.GenerateUpcomingEndingNotificationsAsync();

        var targets = notifications.Added.Select(n => n.UserId).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { SybId, OwnerId }.OrderBy(x => x).ToArray(), targets);
    }

    // ---- Tekrar engeli ----

    // Tarama saat başı çalışıyor. DedupeKey olmasaydı aynı sözleşme için her
    // saat yeni bir bildirim üretilirdi.
    [Fact]
    public async Task Generate_RunTwice_DoesNotDuplicate()
    {
        var (service, _, notifications) = Create(Ending(10));

        await service.GenerateUpcomingEndingNotificationsAsync();
        var countAfterFirst = notifications.Added.Count;

        var inserted = await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.Equal(0, inserted);
        Assert.Equal(countAfterFirst, notifications.Added.Count);
    }

    // Sözleşme bir sonraki eşiğe girdiğinde YENİ bildirim üretilmeli: 30'luk
    // uyarıyı almış olmak, 7 gün kala uyarılmamayı gerektirmez.
    [Fact]
    public async Task Generate_NextThreshold_ProducesNewNotification()
    {
        var (service, contracts, notifications) = Create(Ending(25)); // 30'luk eşik

        await service.GenerateUpcomingEndingNotificationsAsync();
        var afterFirst = notifications.Added.Count;

        contracts.StatusQueryResult = new List<Contract> { Ending(5) }; // artık 7'lik eşik
        var inserted = await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.True(inserted > 0);
        Assert.True(notifications.Added.Count > afterFirst);
    }

    // Tarama yalnızca yürürlükteki sözleşmeleri sorgulamalı; kapanmış kayıtlar
    // için "bitişine 7 gün kaldı" uyarısı üretmek anlamsız olurdu.
    [Fact]
    public async Task Generate_QueriesOnlyLiveStatuses()
    {
        var (service, contracts, _) = Create(Ending(10));

        await service.GenerateUpcomingEndingNotificationsAsync();

        Assert.Equal(new[] { ContractStatus.Aktif, ContractStatus.Uyari },
                     contracts.LastStatusQueryStatuses);
    }
}
