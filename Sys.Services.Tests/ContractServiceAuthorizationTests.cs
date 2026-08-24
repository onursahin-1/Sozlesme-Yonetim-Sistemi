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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.EditContractAsync(contract, personel, "Bedel Değişikliği", "gerekçe", null, null));
    }

    [Fact]
    public async Task EditContractAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestTerminationAsync(contract, personel, "İhbarlı Fesih", DateTime.Today, "gerekçe", null, "Tazminat yok"));
    }

    [Fact]
    public async Task RequestTerminationAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Aktif };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), personel));
    }

    [Fact]
    public async Task FinalizeContractAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var mudur = new User { Role = UserRole.Mudur };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), mudur));
    }

    [Fact]
    public async Task FinalizeContractAsync_Syb_Succeeds()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var syb = new User { Role = UserRole.SYB };

        await service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), syb);

        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
        Assert.Equal(1, contract.Stage);
    }

    [Fact]
    public async Task CreateRequestAsync_Mudur_ThrowsException()
    {
        var service = CreateService(out _);
        var mudur = new User { Id = 7, Role = UserRole.Mudur };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateRequestAsync(new Contract(), mudur));
    }

    [Fact]
    public async Task CreateRequestAsync_Admin_ThrowsException()
    {
        var service = CreateService(out _);
        var admin = new User { Id = 9, Role = UserRole.Admin };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateRequestAsync(new Contract { Id = 1 }, mudur));
    }

    [Fact]
    public async Task AddAttachmentAsync_Admin_ThrowsException()
    {
        var service = CreateService(out _);
        var admin = new User { Id = 9, Role = UserRole.Admin };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectRequestAsync(contract, user, "gerekçe", true));
    }

    [Fact]
    public async Task RejectRequestAsync_EmptyNote_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Talep };
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReportViolationAsync(contract, syb, "Gecikme", DateTime.Today, "açıklama"));
    }

    [Fact]
    public async Task RequestTerminationAsync_AlreadyTerminated_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Status = ContractStatus.Feshedildi };
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveViolationAsync(contract, violation, user, "giderildi"));
    }

    [Fact]
    public async Task ResolveViolationAsync_EmptyNote_ThrowsException()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResolveViolationAsync(contract, violation, syb, "   "));
    }

    [Fact]
    public async Task ResolveViolationAsync_AlreadyResolved_ThrowsException()
    {
        var service = CreateService(out _);
        var (contract, violation) = IhlalliSozlesme();
        violation.ResolvedAt = DateTime.Now;
        var syb = new User { Id = 3, Role = UserRole.SYB };

        await Assert.ThrowsAsync<InvalidOperationException>(
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.FinalizeContractAsync(contract, new List<ContractItem>(), new List<Attachment>(), syb));
    }
}