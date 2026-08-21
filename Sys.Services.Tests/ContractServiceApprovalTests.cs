using System;
using Sys.Domain;
using Sys.Services;
using Xunit;

namespace Sys.Services.Tests;

public class ContractServiceApprovalTests
{
    private static ContractService CreateService(out FakeContractRepository repo)
    {
        repo = new FakeContractRepository();
        return new ContractService(repo, new FakeAttachmentRepository());
    }

    [Fact]
    public async Task Syb_Approves_Stage1_MovesToStage2()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 1, Status = ContractStatus.OnayBekliyor };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Onay, null);

        Assert.Equal(2, contract.Stage);
        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
    }

    [Fact]
    public async Task Mudur_Approves_Stage2_ActivatesContract()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 2, Status = ContractStatus.OnayBekliyor };
        var mudur = new User { Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Onay, null);

        Assert.Equal(3, contract.Stage);
        Assert.Equal(ContractStatus.Aktif, contract.Status);
    }

    [Fact]
    public async Task Syb_Rejects_Stage1_ReturnsToTalep()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 1, Status = ContractStatus.OnayBekliyor };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Red, "eksik belge");

        Assert.Equal(0, contract.Stage);
        Assert.Equal(ContractStatus.Talep, contract.Status);
    }

    [Fact]
    public async Task Mudur_Rejects_Stage2_ReturnsToSybControl()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 2, Status = ContractStatus.OnayBekliyor };
        var mudur = new User { Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Red, "tutarsız bedel");

        Assert.Equal(1, contract.Stage);
        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);

        // Red izi sözleşmenin üzerinde kalmalı: SYB kuyruğunda bunun bir TEKRAR
        // inceleme olduğunu ve gerekçesini görebilsin.
        Assert.True(contract.WasRejected);
        Assert.Equal("tutarsız bedel", contract.LastRejectionNote);
        Assert.NotNull(contract.LastRejectedAt);
    }

    // Zincirde ileri gidildiğinde red izi temizlenir; aksi halde "daha önce reddedildi"
    // uyarısı sözleşme yürürlüğe girdikten sonra da ekranda kalırdı.
    [Fact]
    public async Task Approval_ClearsPreviousRejectionMark()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 1,
            Status = ContractStatus.OnayBekliyor,
            WasRejected = true,
            LastRejectionNote = "tutarsız bedel",
            LastRejectedAt = DateTime.Now
        };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Onay, null);

        Assert.Equal(2, contract.Stage);
        Assert.False(contract.WasRejected);
        Assert.Null(contract.LastRejectionNote);
        Assert.Null(contract.LastRejectedAt);
    }

    [Fact]
    public async Task WrongRole_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 1, Status = ContractStatus.OnayBekliyor };
        var mudur = new User { Role = UserRole.Mudur }; // Stage 1'de SYB beklenir, Müdür değil

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Onay, null));
    }

    [Fact]
    public async Task InvalidStage_ThrowsException()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 3, Status = ContractStatus.Aktif }; // 3 = zaten aktif, onay/red aşaması değil
        var syb = new User { Role = UserRole.SYB };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DecideApprovalAsync(contract, syb, ApprovalDecision.Onay, null));
    }

    [Fact]
    public async Task Termination_Approved_ClosesContract()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 2, Status = ContractStatus.OnayBekliyor, PendingTermination = true };
        var mudur = new User { Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Onay, null);

        Assert.Equal(ContractStatus.Feshedildi, contract.Status);
        Assert.False(contract.PendingTermination);
        Assert.Equal(3, contract.Stage);
    }

    [Fact]
    public async Task Termination_Rejected_ReturnsToActive()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 1, Status = ContractStatus.OnayBekliyor, PendingTermination = true };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Red, "vazgeçildi");

        Assert.Equal(ContractStatus.Aktif, contract.Status);
        Assert.False(contract.PendingTermination);
    }

    // --- Aşağıdaki testler, bu sezon bulunup düzeltilen gerçek bir hatayı
    // (reddedilen "Sözleşme Değiştir" talebinin sözleşmeyi yanlışlıkla Talep
    // durumuna değil, düzenleme öncesi duruma döndürmesi gerektiğini) kalıcı
    // olarak test kapsamına alır — regresyon önleyici testlerdir.

    [Fact]
    public async Task Edit_Approves_Stage1_MovesToStage2()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 1,
            Status = ContractStatus.OnayBekliyor,
            PendingEdit = true,
            PreviousStatusBeforeEdit = ContractStatus.Aktif
        };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Onay, null);

        Assert.Equal(2, contract.Stage);
        Assert.Equal(ContractStatus.OnayBekliyor, contract.Status);
        Assert.True(contract.PendingEdit);
    }

    [Fact]
    public async Task Edit_Approved_Stage2_ActivatesAndClearsPendingEdit()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 2,
            Status = ContractStatus.OnayBekliyor,
            PendingEdit = true,
            PreviousStatusBeforeEdit = ContractStatus.Uyari
        };
        var mudur = new User { Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Onay, null);

        Assert.Equal(3, contract.Stage);
        Assert.Equal(ContractStatus.Aktif, contract.Status);
        Assert.False(contract.PendingEdit);
        Assert.Null(contract.PreviousStatusBeforeEdit);
    }

    [Fact]
    public async Task Edit_Rejected_ReturnsToPreviousStatus_NotTalep()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 1,
            Status = ContractStatus.OnayBekliyor,
            PendingEdit = true,
            PreviousStatusBeforeEdit = ContractStatus.Aktif
        };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Red, "tutar hatalı");

        Assert.Equal(ContractStatus.Aktif, contract.Status);
        Assert.NotEqual(ContractStatus.Talep, contract.Status);
        Assert.False(contract.PendingEdit);
        Assert.Null(contract.PreviousStatusBeforeEdit);
    }

    [Fact]
    public async Task Edit_Rejected_ReturnsToUyariWhenThatWasThePreviousStatus()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 2,
            Status = ContractStatus.OnayBekliyor,
            PendingEdit = true,
            PreviousStatusBeforeEdit = ContractStatus.Uyari
        };
        var mudur = new User { Role = UserRole.Mudur };

        await service.DecideApprovalAsync(contract, mudur, ApprovalDecision.Red, "gerekçe");

        Assert.Equal(ContractStatus.Uyari, contract.Status);
        Assert.False(contract.PendingEdit);
    }

    [Fact]
    public async Task Edit_Rejected_WithoutPreviousStatus_DefaultsToAktif()
    {
        var service = CreateService(out _);
        var contract = new Contract
        {
            Stage = 1,
            Status = ContractStatus.OnayBekliyor,
            PendingEdit = true,
            PreviousStatusBeforeEdit = null
        };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Red, "gerekçe");

        Assert.Equal(ContractStatus.Aktif, contract.Status);
    }

    [Fact]
    public async Task Rejection_RecordsRejectionMetadata()
    {
        var service = CreateService(out _);
        var contract = new Contract { Stage = 1, Status = ContractStatus.OnayBekliyor };
        var syb = new User { Role = UserRole.SYB };

        await service.DecideApprovalAsync(contract, syb, ApprovalDecision.Red, "eksik belge");

        Assert.True(contract.WasRejected);
        Assert.Equal("eksik belge", contract.LastRejectionNote);
        Assert.NotNull(contract.LastRejectedAt);
    }
}