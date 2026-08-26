using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Gösterge panelinin yeni göstergeleri.
//
// Üçü de "sayı doğru mu" değil, "sayı NEYİ sayıyor" sorusuyla ilgili. Yanlış şeyi
// sayan bir gösterge ekranda gayet normal görünür — bu yüzden test ediliyor.
public class DashboardTests
{
    private static ContractService CreateService(out FakeContractRepository repo)
    {
        repo = new FakeContractRepository();
        return new ContractService(repo, new FakeAttachmentRepository());
    }

    private static User Syb() => new() { Id = 3, Role = UserRole.SYB };
    private static User Personel() => new() { Id = 7, Role = UserRole.Personel };

    private static Contract Ending(int id, int daysLeft) => new()
    {
        Id = id,
        Title = $"Sözleşme {id}",
        Status = ContractStatus.Aktif,
        EndDate = DateTime.Today.AddDays(daysLeft),
        CreatedByUserId = 7,
    };

    // ---- Yaklaşan bitişlerde yenileme durumu ----

    [Fact]
    public async Task Upcoming_MarksRenewedContracts()
    {
        var service = CreateService(out var repo);
        repo.StatusQueryResult = new List<Contract> { Ending(1, 10), Ending(2, 20) };
        repo.RenewedIds = new HashSet<int> { 1 };

        var result = await service.GetUpcomingEndingsAsync(Syb());

        Assert.True(result.Single(r => r.Contract.Id == 1).IsRenewed);
        Assert.False(result.Single(r => r.Contract.Id == 2).IsRenewed);
    }

    // Yenileme sorgusu YALNIZCA ekranda görünecek kayıtlar için yapılmalı;
    // tüm sözleşmeler için sormanın anlamı yok.
    [Fact]
    public async Task Upcoming_AsksRenewalOnlyForVisibleContracts()
    {
        var service = CreateService(out var repo);
        repo.StatusQueryResult = new List<Contract>
        {
            Ending(1, 10),
            Ending(2, 20),
            Ending(3, 200),   // eşiğin dışında, listeye girmez
        };

        await service.GetUpcomingEndingsAsync(Syb());

        Assert.Equal(new[] { 1, 2 }, repo.LastRenewalQueryIds!.OrderBy(x => x));
    }

    // Liste boşsa yenileme sorgusu hiç çalıştırılmamalı.
    [Fact]
    public async Task Upcoming_Empty_SkipsRenewalQuery()
    {
        var service = CreateService(out var repo);
        repo.StatusQueryResult = new List<Contract> { Ending(1, 200) };

        var result = await service.GetUpcomingEndingsAsync(Syb());

        Assert.Empty(result);
        Assert.Null(repo.LastRenewalQueryIds);
    }

    // ---- Açık ihlal sayısı ----

    // Kart, durumu "Ihlal" olan SÖZLEŞME sayısını değil AÇIK İHLAL adedini
    // göstermeli: bir sözleşmede üç açık ihlal varken kart "1" derse, aslında
    // üç iş beklediği görünmez.
    [Fact]
    public async Task Dashboard_ReportsOpenViolationCount()
    {
        var service = CreateService(out var repo);
        repo.OpenViolationCount = 5;

        var summary = await service.GetDashboardSummaryAsync(Syb());

        Assert.Equal(5, summary.OpenViolations);
    }

    [Fact]
    public async Task Dashboard_Personel_ScopesOpenViolationsToOwnRecords()
    {
        var service = CreateService(out var repo);

        await service.GetDashboardSummaryAsync(Personel());

        Assert.Equal(7, repo.LastOpenViolationUserId);
    }

    [Fact]
    public async Task Dashboard_Syb_DoesNotScopeOpenViolations()
    {
        var service = CreateService(out var repo);

        await service.GetDashboardSummaryAsync(Syb());

        Assert.Null(repo.LastOpenViolationUserId);
    }

    // ---- Bekleme süresi ----

    [Fact]
    public async Task PendingWork_ReportsOldestWaitingDays()
    {
        var service = CreateService(out var repo);
        repo.StageCount = 4;
        repo.OldestPendingCreatedAt = DateTime.Today.AddDays(-21);

        var summary = await service.GetDashboardSummaryAsync(Syb());

        var item = summary.PendingWork.Single(p => p.NavKey == "sozlesmeKontrol");
        Assert.Equal(21, item.OldestWaitingDays);
    }

    // Bekleyen kayıt yoksa süre de yok; "0 gün" uydurmak yanlış bilgi olurdu.
    [Fact]
    public async Task PendingWork_NoOldestDate_LeavesWaitingNull()
    {
        var service = CreateService(out var repo);
        repo.StageCount = 4;
        repo.OldestPendingCreatedAt = null;

        var summary = await service.GetDashboardSummaryAsync(Syb());

        Assert.Null(summary.PendingWork.Single(p => p.NavKey == "sozlesmeKontrol").OldestWaitingDays);
    }

    // Bekleme süresi kritik olmayan bir ek bilgi: sorgusu patlarsa panel yine
    // açılmalı, yalnızca o satır boş kalmalı.
    [Fact]
    public async Task PendingWork_QueryFails_DashboardStillLoads()
    {
        var service = CreateService(out var repo);
        repo.StageCount = 4;
        repo.ThrowOnOldestPending = true;

        var summary = await service.GetDashboardSummaryAsync(Syb());

        var item = summary.PendingWork.Single(p => p.NavKey == "sozlesmeKontrol");
        Assert.Equal(4, item.Count);
        Assert.Null(item.OldestWaitingDays);
    }
}
