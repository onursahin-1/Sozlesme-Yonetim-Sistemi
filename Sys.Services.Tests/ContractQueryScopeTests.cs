using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

// Sorguların ne kadar veri çektiğine dair testler.
//
// Bu ekranlar eskiden TÜM sözleşmeleri belleğe çekip istemcide süzüyordu. Süzme
// sonucu doğru göründüğü için hata hiç fark edilmiyordu; yalnızca kayıt sayısı
// arttıkça yavaşlıyordu. Bu yüzden testler sonuca değil, servisin depoya HANGİ
// daraltmayla gittiğine bakıyor.
public class ContractQueryScopeTests
{
    private static ContractService CreateService(out FakeContractRepository repo)
    {
        repo = new FakeContractRepository();
        return new ContractService(repo, new FakeAttachmentRepository());
    }

    // ---- Sözleşme Yarat: bekleyen talepler ----

    [Fact]
    public async Task GetPendingRequestsAsync_QueriesOnlyTalepStatus()
    {
        var service = CreateService(out var repo);
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.GetPendingRequestsAsync(syb);

        Assert.Equal(new[] { ContractStatus.Talep }, repo.LastStatusQueryStatuses);
    }

    // Personel yalnızca kendi taleplerini görür; kısıt veritabanına gitmeli,
    // tüm kayıtlar çekilip bellekte süzülmemeli.
    [Fact]
    public async Task GetPendingRequestsAsync_Personel_ScopesToOwnRecords()
    {
        var service = CreateService(out var repo);
        var personel = new User { Id = 7, Role = UserRole.Personel };

        await service.GetPendingRequestsAsync(personel);

        Assert.Equal(7, repo.LastStatusQueryUserId);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_Syb_NoUserScope()
    {
        var service = CreateService(out var repo);
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.GetPendingRequestsAsync(syb);

        Assert.Null(repo.LastStatusQueryUserId);
    }

    // ---- Sözleşme seçici ----

    // Arama ifadesi depoya olduğu gibi iletilmeli: eskiden tüm kayıtlar çekilip
    // kullanıcı listede kaydırarak arıyordu, arama diye bir şey yoktu.
    [Fact]
    public async Task GetContractPickerAsync_PassesSearchTextAndLimit()
    {
        var service = CreateService(out var repo);
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.GetContractPickerAsync(syb, "temizlik");

        Assert.Equal("temizlik", repo.LastPickerSearchText);
        Assert.Equal(ContractService.PickerResultLimit, repo.LastPickerTake);
    }

    [Fact]
    public async Task GetContractPickerAsync_Personel_ScopesToOwnRecords()
    {
        var service = CreateService(out var repo);
        var personel = new User { Id = 7, Role = UserRole.Personel };

        await service.GetContractPickerAsync(personel, null);

        Assert.Equal(7, repo.LastPickerUserId);
    }

    // ---- Liste durum filtresi ----

    // "Yürürlükte" TEK bir durum değil. Bu düğme eskiden yalnızca Status = Aktif
    // olanları getiriyordu; bitişi yaklaşan bir sözleşme (Uyarı) listeden düşüyordu,
    // oysa yükümlülükleri sürüyor. Panelin değer/dağılım kutuları ve
    // düzenleme/fesih/ihlal ekranları zaten üç durumun tamamına bakıyordu.
    [Fact]
    public async Task GetContractsPagedAsync_YururlukteFilter_IncludesAllLiveStatuses()
    {
        var service = CreateService(out var repo);
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.GetContractsPagedAsync(syb, "yururlukte", null, 1, 10, null);

        Assert.Equal(
            new[] { ContractStatus.Aktif, ContractStatus.Uyari, ContractStatus.Ihlal },
            repo.LastPagedIncludeStatuses);
    }

    // Alt kümeler ayrı ayrı da seçilebilmeli.
    [Theory]
    [InlineData("uyari", ContractStatus.Uyari)]
    [InlineData("ihlal", ContractStatus.Ihlal)]
    public async Task GetContractsPagedAsync_SubsetFilter_NarrowsToSingleStatus(string key, ContractStatus expected)
    {
        var service = CreateService(out var repo);
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.GetContractsPagedAsync(syb, key, null, 1, 10, null);

        Assert.Equal(new[] { expected }, repo.LastPagedIncludeStatuses);
    }

    // ---- Onay kuyruğu rozeti ----

    // Rozet yalnızca bir sayı gösteriyor; eskiden bekleyen sözleşmelerin tamamı
    // çekilip .Count alınıyordu.
    [Fact]
    public async Task GetPendingApprovalCountAsync_Syb_ReturnsStageCount()
    {
        var service = CreateService(out var repo);
        repo.StageCount = 12;
        var syb = new User { Id = 3, Role = UserRole.SYB };

        Assert.Equal(12, await service.GetPendingApprovalCountAsync(syb));
    }

    // Onay kuyruğu olmayan roller için sorgu hiç çalışmamalı.
    [Theory]
    [InlineData(UserRole.Personel)]
    [InlineData(UserRole.Admin)]
    public async Task GetPendingApprovalCountAsync_RoleWithoutQueue_ReturnsZero(UserRole role)
    {
        var service = CreateService(out var repo);
        repo.StageCount = 12;
        var user = new User { Id = 1, Role = role };

        Assert.Equal(0, await service.GetPendingApprovalCountAsync(user));
    }
}
