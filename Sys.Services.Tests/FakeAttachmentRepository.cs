using Sys.Domain;
using Sys.Services;

namespace Sys.Services.Tests;

public class FakeAttachmentRepository : IAttachmentRepository
{
    public Task AddAsync(Attachment attachment) => Task.CompletedTask;
}