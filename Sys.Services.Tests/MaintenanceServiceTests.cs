using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Saatlik bakım işi.
//
// Bu iş her kullanıcının bilgisayarında ayrı ayrı tetikleniyor; aynı saat
// diliminde yalnızca birinin çalışması gerekiyor. Kilit mantığı bozulursa ya iş
// hiç çalışmaz ya da herkes aynı anda çalıştırır — ikisi de ekranda hiçbir
// belirti vermez, bu yüzden test ediliyor.
public class MaintenanceServiceTests
{
    private static MaintenanceService Create(
        out FakeScheduledJobRepository jobs,
        out FakeContractRepository contracts)
    {
        jobs = new FakeScheduledJobRepository();
        contracts = new FakeContractRepository();

        var users = new FakeUserRepository();
        var notificationRepo = new FakeNotificationRepository();

        var contractService = new ContractService(contracts, new FakeAttachmentRepository());
        var notificationService = new NotificationService(notificationRepo, contracts, users);

        return new MaintenanceService(jobs, contractService, notificationService);
    }

    // ---- Kilit ----

    [Fact]
    public async Task Run_WhenLockAcquired_ReturnsTrue()
    {
        var service = Create(out var jobs, out _);
        jobs.CanAcquire = true;

        Assert.True(await service.RunHourlyMaintenanceAsync());
    }

    // Kilit alınamadıysa (başkası yaptı ya da vakti gelmedi) hiçbir iş
    // yapılmamalı — aksi halde her istemci aynı taramayı tekrarlardı.
    [Fact]
    public async Task Run_WhenLockNotAcquired_DoesNoWork()
    {
        var service = Create(out var jobs, out var contracts);
        jobs.CanAcquire = false;

        var ran = await service.RunHourlyMaintenanceAsync();

        Assert.False(ran);
        Assert.Equal(0, contracts.ReconcileCallCount);
        Assert.Equal(0, jobs.ReleaseCallCount);
    }

    // İş satırı yoksa oluşturulmalı; kilit denemesi ondan sonra gelir.
    [Fact]
    public async Task Run_EnsuresJobRowBeforeAcquiring()
    {
        var service = Create(out var jobs, out _);

        await service.RunHourlyMaintenanceAsync();

        Assert.Equal(1, jobs.EnsureCallCount);
        Assert.Equal(1, jobs.AcquireCallCount);
    }

    // ---- Sonuç kaydı ----

    // Ne yapıldığı tabloya yazılır; sorun çıktığında sonradan bakılabilsin.
    [Fact]
    public async Task Run_WritesSummaryOnRelease()
    {
        var service = Create(out var jobs, out var contracts);
        contracts.ReconcileResult = 4;

        await service.RunHourlyMaintenanceAsync();

        Assert.Equal(1, jobs.ReleaseCallCount);
        Assert.Contains("4 sözleşme", jobs.LastResult);
    }

    // ---- Hata yolu ----

    // EN KRİTİK DAVRANIŞ: hata olsa bile kilit bırakılmalı. Bırakılmazsa iş,
    // kilit süresi dolana kadar (10 dk) askıda kalır ve bu ekranda hiçbir
    // belirti vermez.
    [Fact]
    public async Task Run_OnError_StillReleasesLock()
    {
        var service = Create(out var jobs, out var contracts);
        contracts.ThrowOnReconcile = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunHourlyMaintenanceAsync());

        Assert.Equal(1, jobs.ReleaseCallCount);
    }

    // Hata özeti de tabloya yazılır ki sonradan görülebilsin.
    [Fact]
    public async Task Run_OnError_WritesErrorSummary()
    {
        var service = Create(out var jobs, out var contracts);
        contracts.ThrowOnReconcile = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunHourlyMaintenanceAsync());

        Assert.StartsWith("Hata:", jobs.LastResult);
    }
}
