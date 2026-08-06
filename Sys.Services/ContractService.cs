using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sys.Domain;

namespace Sys.Services;

public class DashboardStats
{
    public int Aktif { get; set; }
    public int OnayBekliyor { get; set; }
    public int Uyari { get; set; }
    public int Ihlal { get; set; }
}

public class ContractService
{
    private readonly IContractRepository _contracts;
    private readonly IAttachmentRepository _attachments;

    public ContractService(IContractRepository contracts, IAttachmentRepository attachments)
    {
        _contracts = contracts;
        _attachments = attachments;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(User currentUser)
    {
        var contracts = currentUser.Role == UserRole.Personel
            ? await _contracts.GetByCreatedUserAsync(currentUser.Id)
            : await _contracts.GetAllAsync();

        return new DashboardStats
        {
            Aktif = contracts.Count(c => c.Status == ContractStatus.Aktif),
            OnayBekliyor = contracts.Count(c => c.Status == ContractStatus.OnayBekliyor),
            Uyari = contracts.Count(c => c.Status == ContractStatus.Uyari),
            Ihlal = contracts.Count(c => c.Status == ContractStatus.Ihlal),
        };
    }

    public async Task<List<Contract>> GetContractsAsync(User currentUser)
    {
        return currentUser.Role == UserRole.Personel
            ? await _contracts.GetByCreatedUserAsync(currentUser.Id)
            : await _contracts.GetAllAsync();
    }

    public async Task<Contract> CreateRequestAsync(Contract contract)
    {
        contract.Status = ContractStatus.Talep;
        contract.Stage = 0;
        contract.CreatedAt = DateTime.Now;
        await _contracts.AddAsync(contract);
        return contract;
    }

    public async Task AddAttachmentAsync(Attachment attachment)
    {
        await _attachments.AddAsync(attachment);
    }
}