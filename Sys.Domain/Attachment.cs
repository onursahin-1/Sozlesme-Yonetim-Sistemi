namespace Sys.Domain;

public class Attachment
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public AttachmentCategory Category { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int UploadedByUserId { get; set; }
}

public enum AttachmentCategory
{
    Talep,
    Sozlesme,
    Ek,
    Teminat,
    Ihlal,
    Fesih
}