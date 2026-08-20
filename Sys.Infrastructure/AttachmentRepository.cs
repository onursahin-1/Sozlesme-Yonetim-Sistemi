using Microsoft.EntityFrameworkCore;
using Sys.Domain;
using Sys.Services;

namespace Sys.Infrastructure;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly string _connectionString;

    public AttachmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(Attachment attachment)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync();
    }

    public async Task<Attachment?> GetByIdAsync(int id)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        return await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task DeleteAsync(int attachmentId)
    {
        using var db = DbConnectionFactory.CreateContext(_connectionString);
        var tracked = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (tracked is null) return;
        db.Attachments.Remove(tracked);
        await db.SaveChangesAsync();
    }
}