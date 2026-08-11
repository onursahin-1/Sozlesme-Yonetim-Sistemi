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
}