using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class FakeAttachmentRepository : IAttachmentRepository
{
    public Task AddAsync(Attachment attachment) => Task.CompletedTask;
    public Task<Attachment?> GetByIdAsync(int id) => Task.FromResult<Attachment?>(null);
    public Task DeleteAsync(int attachmentId) => Task.CompletedTask;
}