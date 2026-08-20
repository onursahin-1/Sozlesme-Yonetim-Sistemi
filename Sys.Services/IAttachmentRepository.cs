using Sys.Domain;

namespace Sys.Services;

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment);
    Task<Attachment?> GetByIdAsync(int id);
    Task DeleteAsync(int attachmentId);
}