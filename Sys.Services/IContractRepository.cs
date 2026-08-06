using Sys.Domain;

namespace Sys.Services;

public interface IContractRepository
{
    Task<List<Contract>> GetAllAsync();
    Task<List<Contract>> GetByCreatedUserAsync(int userId);
    Task AddAsync(Contract contract);
    Task FinalizeCreationAsync(Contract contract, List<ContractItem> items, List<Attachment> attachments, AuditLog auditLog);
}