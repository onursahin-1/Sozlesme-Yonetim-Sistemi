using Sys.Domain;

namespace Sys.Services;

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment);
}