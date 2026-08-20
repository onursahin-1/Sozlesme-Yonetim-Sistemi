using Sys.Domain;

namespace Sys.Services;

// Sözleşme akışı dışındaki (hesap yönetimi, kimlik doğrulama) olayların denetim
// kaydına yazılması için. Sözleşme tarafı kendi kayıtlarını IContractRepository
// üzerinden, ilgili işlemle aynı transaction içinde yazmaya devam eder.
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
}
