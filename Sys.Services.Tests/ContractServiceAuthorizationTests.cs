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
}