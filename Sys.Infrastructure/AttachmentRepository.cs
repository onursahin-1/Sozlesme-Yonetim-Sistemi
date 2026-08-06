using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly SysDbContext _db;

    public AttachmentRepository(SysDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Attachment attachment)
    {
        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();
    }
}