using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class ContractRepository : IContractRepository
{
    private readonly SysDbContext _db;

    public ContractRepository(SysDbContext db)
    {
        _db = db;
    }

    public Task<List<Contract>> GetAllAsync()
        => _db.Contracts.ToListAsync();

    public Task<List<Contract>> GetByCreatedUserAsync(int userId)
        => _db.Contracts.Where(c => c.CreatedByUserId == userId).ToListAsync();
}