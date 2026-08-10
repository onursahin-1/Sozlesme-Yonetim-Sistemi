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
}