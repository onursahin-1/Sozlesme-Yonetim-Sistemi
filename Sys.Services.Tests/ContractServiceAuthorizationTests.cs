using System;
using System.Collections.Generic;
using Sys.Domain;
using Sys.Services;
using Xunit;

namespace Sys.Services.Tests;

public class ContractServiceAuthorizationTests
{
    private static ContractService CreateService(out FakeContractRepository repo)
    {
        repo = new FakeContractRepository();
        return new ContractService(repo, new FakeAttachmentRepository());
    }

    [Fact]
    public async Task EditContractAsync_Personel_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var personel = new User { Role = UserRole.Personel };

        await Assert.ThrowsAsync<AppException>(
            () => service.EditContractAsync(contract, personel, "Bedel Değişikliği", "gerekçe", null, null));
    }

    [Fact]
    public async Task EditContractAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.EditContractAsync(contract, mudur, "Bedel Değişikliği", "gerekçe", null, null));
    }

    [Fact]
    public async Task EditContractAsync_Syb_Succeeds()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var syb = new User { Role = UserRole.SYB };

        await service.EditContractAsync(contract, syb, "Bedel Değişikliği", "gerekçe", null, null);

        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
        Assert.Equal(1, contract.Stage);
    }

    [Fact]
    public async Task ReportViolationAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.ReportViolationAsync(contract, mudur, "Gecikme", DateTime.Today, "açıklama"));
    }

    [Fact]
    public async Task ReportViolationAsync_Personel_Succeeds()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var personel = new User { Role = UserRole.Personel };

        await service.ReportViolationAsync(contract, personel, "Gecikme", DateTime.Today, "açıklama");

        Assert.Equal(ContractStatus.Ihlal, contract.Status);
    }

    [Fact]
    public async Task ReportViolationAsync_Syb_Succeeds()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var syb = new User { Role = UserRole.SYB };

        await service.ReportViolationAsync(contract, syb, "Gecikme", DateTime.Today, "açıklama");

        Assert.Equal(ContractStatus.Ihlal, contract.Status);
    }

    [Fact]
    public async Task RequestTerminationAsync_Personel_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var personel = new User { Role = UserRole.Personel };

        await Assert.ThrowsAsync<AppException>(
            () => service.RequestTerminationAsync(contract, personel, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok"));
    }

    [Fact]
    public async Task RequestTerminationAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.RequestTerminationAsync(contract, mudur, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok"));
    }

    [Fact]
    public async Task RequestTerminationAsync_Syb_Succeeds()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var syb = new User { Role = UserRole.SYB };

        await service.RequestTerminationAsync(contract, syb, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok");

        Assert.True(contract.PendingTermination);
    }

    [Fact]
    public async Task FinalizeContractAsync_Personel_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var personel = new User { Role = UserRole.Personel };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), personel));
    }

    [Fact]
    public async Task FinalizeContractAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), mudur));
    }

    // Sözleşmeye dönüştürülmeye hazır talep: tarihleri ve en az bir kalemi var.
    //
    // Talebi PERSONEL (Id = 7) açmış sayılıyor. Kimin açtığı burada önemli, çünkü
    // talebi sözleşmeyi oluşturan SYB'nin kendisi açtıysa Son Kontrol atlanıyor.
    // Alan atanmadan bırakılsaydı 0 kalır ve Id'si atanmamış bir test kullanıcısıyla
    // tesadüfen eşleşirdi — test, ölçmek istemediği yolu ölçerdi.
    private static Contract HazirTalep() => new()
    {
        Status = ContractStatus.Talep,
        CreatedByUserId = 7,
        StartDate = DateTime.Today,
        EndDate = DateTime.Today.AddYears(1)
    };

    private static List<ContractItem> BirKalem() => new()
    {
        new ContractItem { Description = "Hizmet", Quantity = 1, Unit = "adet", UnitPrice = 1000m }
    };

    [Fact]
    public async Task FinalizeContractAsync_Syb_Succeeds()
    {
        var service = CreateService(out _);
        var contract = HazirTalep();
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.FinalizeContractAsync(contract, BirKalem(), new List<Attachment>(), syb);

        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
        Assert.Equal(1, contract.Stage);
        Assert.Equal(1000m, contract.TotalAmount);
    }

    // Tarih doğrulaması sadece sihirbaz ekranındaydı; servis hiç bakmıyordu.
    // Tarihsiz bir sözleşme "Aktif" olabiliyor, kalan gün hesaplanamıyor ve bakım
    // işi süresi dolmuşları hiç göremiyordu.
    [Fact]
    public async Task FinalizeContractAsync_WithoutDates_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, BirKalem(), new List<Attachment>(), syb));
    }

    [Fact]
    public async Task FinalizeContractAsync_EndBeforeStart_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = HazirTalep();
        contract.EndDate = contract.StartDate;
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, BirKalem(), new List<Attachment>(), syb));
    }

    // Kalemsiz sözleşmenin bedeli sıfır olurdu.
    [Fact]
    public async Task FinalizeContractAsync_WithoutItems_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = HazirTalep();
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), syb));
    }

    [Fact]
    public async Task CreateRequestAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var mudur = new User { Id = 7, Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.CreateRequestAsync(new Contract(), mudur));
    }

    [Fact]
    public async Task CreateRequestAsync_Admin_ThrowsException()
    {
        var service = CreateService(out _);
        var admin = new User { Id = 9, Role = UserRole.Admin };

        await Assert.ThrowsAsync<AppException>(
            () => service.CreateRequestAsync(new Contract(), admin));
    }

    // Talebin sahibi ve başlangıç durumu çağıranın gönderdiği değere güvenilerek değil,
    // servis içinde belirlenir; aksi halde bir talep başka bir kullanıcının üzerine
    // yazılabilir ya da doğrudan ileri bir aşamada başlatılabilirdi.
    [Fact]
    public async Task CreateRequestAsync_OverridesCallerSuppliedOwnerAndState()
    {
        var service = CreateService(out _);
        var personel = new User { Id = 42, Role = UserRole.Personel };

        var contract = new Contract
        {
            Id = 999,
            CreatedByUserId = 1,           // başka bir kullanıcı
            Status = ContractStatus.Aktif, // aşama atlatma denemesi
            Stage = 2,
            Title = "Test",
        };

        var result = await service.CreateRequestAsync(contract, personel);

        Assert.Equal(0, result.Id);
        Assert.Equal(42, result.CreatedByUserId);
        Assert.Equal(ContractStatus.Talep, result.Status);
        Assert.Equal(0, result.Stage);
    }

    [Fact]
    public async Task UpdateRequestAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var mudur = new User { Id = 7, Role = UserRole.Mudur };

        await Assert.ThrowsAsync<AppException>(
            () => service.UpdateRequestAsync(new Contract { Id = 1 }, mudur));
    }

    [Fact]
    public async Task AddAttachmentAsync_Admin_ThrowsException()
    {
        var service = CreateService(out _);
        var admin = new User { Id = 9, Role = UserRole.Admin };

        await Assert.ThrowsAsync<AppException>(
            () => service.AddAttachmentAsync(new Attachment { ContractId = 1 }, admin));
    }

    // Kullanıcı göremediği bir sözleşmeye dosya ekleyemez. FakeContractRepository
    // GetByIdWithDetailsAsync için null döndüğü için burada "sözleşme bulunamadı"
    // yolu sınanıyor; gerçek repoda Personel'in başkasının sözleşmesi de null döner.
    [Fact]
    public async Task AddAttachmentAsync_UnreachableContract_ThrowsException()
    {
        var service = CreateService(out _);
        var personel = new User { Id = 42, Role = UserRole.Personel };

        await Assert.ThrowsAsync<AppException>(
            () => service.AddAttachmentAsync(new Attachment { ContractId = 123 }, personel));
    }

    // --- Talep reddi (Stage 0) ---

    [Theory]
    [InlineData(UserRole.Personel)]
    [InlineData(UserRole.Mudur)]
    [InlineData(UserRole.Admin)]
    public async Task RejectRequestAsync_NonSyb_ThrowsException(UserRole role)
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var user = new User { Id = 5, Role = role };

        await Assert.ThrowsAsync<AppException>(
            () => service.RejectRequestAsync(contract, user, "gerekçe", true));
    }

    [Fact]
    public async Task RejectRequestAsync_EmptyNote_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.RejectRequestAsync(contract, syb, "   ", true));
    }

    // Onay zincirine girmiş bir sözleşme bu yoldan kapatılamamalı; kararı
    // DecideApprovalAsync vermeli.
    [Fact]
    public async Task RejectRequestAsync_AlreadyContract_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.OnayBekliyor, Stage = 1 };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.RejectRequestAsync(contract, syb, "gerekçe", false));
    }

    // İade: talep sahibine geri döner, düzeltilip yeniden gönderilebilir.
    [Fact]
    public async Task RejectRequestAsync_AllowResubmit_KeepsRequestOpen()
    {
        var service = CreateService(out _);
        var contract = new Contract { Id = 1, Status = ContractStatus.Talep, Title = "Test" };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.RejectRequestAsync(contract, syb, "eksik bilgi", allowResubmit: true);

        Assert.Equal(ContractStatus.Talep, contract.Status);
        Assert.Equal(0, contract.Stage);
        Assert.True(contract.WasRejected);
        Assert.Equal("eksik bilgi", contract.LastRejectionNote);
    }

    // Kapatma: talep nihai olarak reddedilir.
    [Fact]
    public async Task RejectRequestAsync_WithoutResubmit_ClosesRequest()
    {
        var service = CreateService(out _);
        var contract = new Contract { Id = 1, Status = ContractStatus.Talep, Title = "Test" };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.RejectRequestAsync(contract, syb, "mükerrer talep", allowResubmit: false);

        Assert.Equal(ContractStatus.Reddedildi, contract.Status);
        Assert.Equal(0, contract.Stage);
        Assert.True(contract.WasRejected);
    }

    // --- Yürürlük kontrolü (düzenleme / fesih / ihlal) ---

    // Onay zincirinin ortasındaki sözleşme düzenlemeye açılamamalı: gerçek veride bu
    // yüzden "Onay Bekliyor / Stage 3" gibi hiçbir kuyrukta görünmeyen kayıt oluştu.
    [Theory]
    [InlineData(ContractStatus.Talep)]
    [InlineData(ContractStatus.OnayBekliyor)]
    [InlineData(ContractStatus.Tamamlandi)]
    [InlineData(ContractStatus.Feshedildi)]
    [InlineData(ContractStatus.Reddedildi)]
    public async Task EditContractAsync_NotLiveContract_ThrowsException(ContractStatus status)
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = status };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.EditContractAsync(contract, syb, "Bedel Değişikliği", "gerekçe", null, null));
    }

    // Arşivdeki bir sözleşmeye ihlal bildirilirse Status = Ihlal atanıp kayıt
    // yürürlükteymiş gibi listeye geri dönüyordu.
    [Theory]
    [InlineData(ContractStatus.Tamamlandi)]
    [InlineData(ContractStatus.Feshedildi)]
    public async Task ReportViolationAsync_ArchivedContract_ThrowsException(ContractStatus status)
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = status };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.ReportViolationAsync(contract, syb, "Gecikme", DateTime.Today, "açıklama"));
    }

    [Fact]
    public async Task RequestTerminationAsync_AlreadyTerminated_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Feshedildi };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.RequestTerminationAsync(contract, syb, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok"));
    }

    // Devam eden bir düzenleme varken ikinci bir işlem başlatılamaz: geri dönüş
    // noktasını tutan PreviousStatusBeforeEdit tek değer olduğu için ikincisi
    // birincinin kaydını eziyordu.
    [Fact]
    public async Task RequestTerminationAsync_PendingEdit_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif, PendingEdit = true };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.RequestTerminationAsync(contract, syb, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok"));
    }

    // --- İhlalin giderilmesi ---

    private static (Contract Contract, Violation Violation) IhlalliSozlesme(DateTime? endDate = null)
    {
        var violation = new Violation { Id = 1, ViolationType = "Gecikme", ViolationDate = DateTime.Today };
        var contract = new Contract
        {
            Id = 1,
            Title = "Test",
            Status = ContractStatus.Ihlal,
            EndDate = endDate ?? DateTime.Today.AddYears(1),
            Violations = { violation }
        };
        return (contract, violation);
    }

    [Theory]
    [InlineData(UserRole.Personel)]
    [InlineData(UserRole.Mudur)]
    [InlineData(UserRole.Admin)]
    public async Task ResolveViolationAsync_NonSyb_ThrowsException(UserRole role)
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        var user = new User { Id = 5, Role = role };

        await Assert.ThrowsAsync<AppException>(
            () => service.ResolveViolationAsync(contract, violation, user, "giderildi"));
    }

    [Fact]
    public async Task ResolveViolationAsync_EmptyNote_ThrowsException()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.ResolveViolationAsync(contract, violation, syb, "   "));
    }

    [Fact]
    public async Task ResolveViolationAsync_AlreadyResolved_ThrowsException()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        violation.ResolvedAt = DateTime.Now;
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.ResolveViolationAsync(contract, violation, syb, "tekrar"));
    }

    // Son açık ihlal kapanınca sözleşme yürürlüğe döner.
    [Fact]
    public async Task ResolveViolationAsync_LastOpen_ReturnsContractToActive()
    {
        var service = CreateService(out var repo);
        var (contract, violation) = IhlalliSozlesme();
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.ResolveViolationAsync(contract, violation, syb, "eksik iş tamamlandı");

        Assert.True(violation.IsResolved);
        Assert.Equal(3, violation.ResolvedByUserId);
        Assert.Equal("eksik iş tamamlandı", violation.ResolutionNote);
        Assert.Equal(ContractStatus.Aktif, contract.Status);
        Assert.Same(violation, repo.LastResolvedViolation);
    }

    // Bitişi yaklaşmış sözleşme Aktif'e değil Uyarı'ya döner (bakım işiyle aynı eşik).
    [Fact]
    public async Task ResolveViolationAsync_EndingSoon_ReturnsToWarning()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme(DateTime.Today.AddDays(10));
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.ResolveViolationAsync(contract, violation, syb, "telafi edildi");

        Assert.Equal(ContractStatus.Uyari, contract.Status);
    }

    // Başka açık ihlal varken durum korunur.
    [Fact]
    public async Task ResolveViolationAsync_OtherOpenViolations_KeepsIhlalStatus()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        contract.Violations.Add(new Violation { Id = 2, ViolationType = "Kalite", ViolationDate = DateTime.Today });
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await service.ResolveViolationAsync(contract, violation, syb, "biri giderildi");

        Assert.True(violation.IsResolved);
        Assert.Equal(ContractStatus.Ihlal, contract.Status);
    }

    // Kapatılmış bir talep sözleşmeye dönüştürülememeli.
    [Fact]
    public async Task FinalizeContractAsync_RejectedRequest_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Reddedildi };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<AppException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), syb));
    }

    // ---- Sözleşme yenileme ----

    // Yürürlükteki ve süresi dolmuş sözleşmeler yenilenebilir.
    [Theory]
    [InlineData(ContractStatus.Aktif)]
    [InlineData(ContractStatus.Uyari)]
    [InlineData(ContractStatus.Ihlal)]
    [InlineData(ContractStatus.Tamamlandi)]
    public void CanRenew_LiveOrCompleted_ReturnsTrue(ContractStatus status)
    {
        var contract = new Contract { Status = status };
        var personel = new User { Id = 1, Role = UserRole.Personel };

        Assert.True(ContractService.CanRenew(contract, personel));
    }

    // Feshedilen sözleşme yenilenemez: taraflar ilişkiyi bilerek sonlandırdı,
    // yeniden çalışılacaksa bu sıfırdan verilmesi gereken bir karar.
    // Henüz sözleşmeye dönüşmemiş kayıtlar da yenilenemez — ortada yenilenecek
    // bir dönem yok.
    [Theory]
    [InlineData(ContractStatus.Feshedildi)]
    [InlineData(ContractStatus.Reddedildi)]
    [InlineData(ContractStatus.Talep)]
    [InlineData(ContractStatus.OnayBekliyor)]
    public void CanRenew_ClosedOrNotYetContract_ReturnsFalse(ContractStatus status)
    {
        var contract = new Contract { Status = status };
        var personel = new User { Id = 1, Role = UserRole.Personel };

        Assert.False(ContractService.CanRenew(contract, personel));
    }

    // Yenileme yeni bir talep açmak demek; talep açamayan rol yenileme de yapamaz.
    [Theory]
    [InlineData(UserRole.Mudur)]
    [InlineData(UserRole.Admin)]
    public void CanRenew_RoleCannotCreateRequests_ReturnsFalse(UserRole role)
    {
        var contract = new Contract { Status = ContractStatus.Tamamlandi };
        var user = new User { Id = 1, Role = role };

        Assert.False(ContractService.CanRenew(contract, user));
    }

    // Yenileme talebi KAYNAK sözleşmeye dokunmaz: yeni bir kayıt doğar, eski
    // sözleşme kendi durumunda ve kendi döneminde kalır.
    [Fact]
    public async Task CreateRequestAsync_Renewal_LinksSourceAndLeavesItUntouched()
    {
        var service = CreateService(out var repo);
        var source = new Contract { Id = 41, Status = ContractStatus.Tamamlandi, TotalAmount = 5000m };
        var personel = new User { Id = 1, Role = UserRole.Personel };

        var renewal = new Contract
        {
            Title = "Temizlik Hizmeti 2027",
            CompanyName = "ABC A.Ş.",
            RenewedFromContractId = source.Id,
        };

        var saved = await service.CreateRequestAsync(renewal, personel);

        Assert.Equal(source.Id, saved.RenewedFromContractId);
        Assert.Equal(ContractStatus.Talep, saved.Status);
        Assert.NotEqual(source.Id, saved.Id);

        // Kaynak sözleşme değişmedi.
        Assert.Equal(ContractStatus.Tamamlandi, source.Status);
        Assert.Equal(5000m, source.TotalAmount);
    }

    // ---- Son Kontrol'ün atlanması ----
    //
    // Kural: talebi açan kişi ile sözleşmeyi oluşturan AYNI SYB ise Son Kontrol
    // atlanır. Aksi halde aynı kişi kendi girdiği veriyi kendisi onaylamış olurdu.
    // Talep başkasından geldiyse akış değişmez.

    private static (Contract Contract, List<ContractItem> Items) HazirTalep(int createdByUserId)
    {
        var contract = new Contract
        {
            Id = 5,
            Title = "Temizlik Hizmeti",
            Status = ContractStatus.Talep,
            Stage = 0,
            CreatedByUserId = createdByUserId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
        };
        var items = new List<ContractItem>
        {
            new() { Description = "Aylık hizmet", Quantity = 12, Unit = "Ay", UnitPrice = 1000m }
        };
        return (contract, items);
    }

    [Fact]
    public async Task FinalizeContractAsync_OwnRequest_SkipsFinalCheck()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: syb.Id);

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.Equal(2, contract.Stage);   // doğrudan yönetim onayı
        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
    }

    // Personel'in açtığı talepten doğan sözleşme Son Kontrol'e uğramaya devam eder.
    [Fact]
    public async Task FinalizeContractAsync_RequestFromSomeoneElse_KeepsFinalCheck()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: 7); // Personel

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.Equal(1, contract.Stage);   // Son Kontrol
    }

    // Başka bir SYB'nin talebi de "kendi talebi" sayılmaz: kararı veren kişi
    // veriyi girenden farklıysa kontrol anlamlıdır.
    [Fact]
    public async Task FinalizeContractAsync_RequestFromAnotherSyb_KeepsFinalCheck()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: 4); // başka bir SYB

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.Equal(1, contract.Stage);
    }

    // ---- Reddedilen sözleşme nereye döner? ----
    //
    // Son Kontrol ekranında düzeltme yapılamaz; orada yalnızca onay ve red vardır.
    // Bu yüzden Müdür reddi ancak Son Kontrol'ü BAŞKASI yapacaksa Stage 1'e döner.
    // Atlanmış bir sözleşmede öyle bir mercii yok: Stage 1'e dönmek SYB'yi kendi
    // sözleşmesini onaylayan bir ekrana düşürüyor, düzeltebilmek için kendi
    // talebini reddetmek zorunda bırakıyordu.

    [Fact]
    public async Task FinalizeContractAsync_OwnRequest_RecordsSkipOnContract()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: syb.Id);

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        // Karar sözleşmede saklanmalı: red anında yeniden hesaplanamıyor, çünkü
        // o an işlemi yapan kişi Müdür.
        Assert.True(contract.FinalCheckSkipped);
    }

    [Fact]
    public async Task FinalizeContractAsync_RequestFromSomeoneElse_DoesNotRecordSkip()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: 7);

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.False(contract.FinalCheckSkipped);
    }

    [Fact]
    public async Task DecideApprovalAsync_MudurRejects_SkippedContract_ReturnsToTalep()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Id = 5,
            Title = "Temizlik Hizmeti",
            Status = ContractStatus.OnayBekliyor,
            Stage = 2,
            CreatedByUserId = 3,
            FinalCheckSkipped = true
        };
        var mudur = new User { Id = 9, Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Red, "bedel yüksek");

        Assert.Equal(0, contract.Stage);
        Assert.Equal(ContractStatus.Talep, contract.Status);
        Assert.True(contract.WasRejected);
        Assert.Equal("bedel yüksek", contract.LastRejectionNote);
    }

    [Fact]
    public async Task DecideApprovalAsync_MudurRejects_NormalContract_ReturnsToFinalCheck()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Id = 5,
            Title = "Temizlik Hizmeti",
            Status = ContractStatus.OnayBekliyor,
            Stage = 2,
            CreatedByUserId = 7,
            FinalCheckSkipped = false
        };
        var mudur = new User { Id = 9, Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Red, "bedel yüksek");

        Assert.Equal(1, contract.Stage);
        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
    }

    // Reddin HANGİ aşamadan geldiği kayda geçmeli. Bu bilgi olmadan üç farklı olay
    // ekranda aynı görünüyor: SYB'nin iadesi, Müdür'ün geri göndermesi ve kişinin
    // kendi talebini geri çekmesi — üçü de "Talep + reddedilmiş".
    [Fact]
    public async Task DecideApprovalAsync_MudurRejects_RecordsRejectingStage()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Id = 5,
            Title = "Temizlik Hizmeti",
            Status = ContractStatus.OnayBekliyor,
            Stage = 2,
            CreatedByUserId = 3,
            FinalCheckSkipped = true
        };
        var mudur = new User { Id = 9, Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Red, "bedel yüksek");

        Assert.Equal(2, contract.LastRejectedStage);
    }

    [Fact]
    public async Task RejectRequestAsync_RecordsStageZero()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var contract = new Contract
        {
            Id = 5,
            Title = "Temizlik Hizmeti",
            Status = ContractStatus.Talep,
            CreatedByUserId = syb.Id,
            FinalCheckSkipped = true   // daha önce Müdür'den dönmüş bir kayıt
        };

        await service.RejectRequestAsync(contract, syb, "vazgeçtim", allowResubmit: true);

        // Müdür'ün reddi değil, kişinin kendi geri çekmesi. FinalCheckSkipped hâlâ
        // true — o alan bu soruya cevap vermiyor, ekran ondan okumamalı.
        Assert.Equal(0, contract.LastRejectedStage);
    }

    [Fact]
    public async Task FinalizeContractAsync_ClearsRejectingStage()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: syb.Id);
        contract.WasRejected = true;
        contract.LastRejectedStage = 2;

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.Null(contract.LastRejectedStage);
    }

    // Reddedilen sözleşme yeniden gönderildiğinde Müdür'e dönmeli; atlama kararı
    // yeniden hesaplanıyor, eski değere yapışıp kalmıyor.
    [Fact]
    public async Task FinalizeContractAsync_AfterRejection_SkipsFinalCheckAgain()
    {
        var service = CreateService(out _);
        var syb = new User { Id = 3, Role = UserRole.SYB };
        var (contract, items) = HazirTalep(createdByUserId: syb.Id);
        contract.WasRejected = true;
        contract.LastRejectionNote = "bedel yüksek";

        await service.FinalizeContractAsync(contract, items, new List<Attachment>(), syb);

        Assert.Equal(2, contract.Stage);
        Assert.True(contract.FinalCheckSkipped);
        Assert.False(contract.WasRejected);   // yeniden gönderildi, red izi temizlendi
    }
}
