using System.Net.Mail;

namespace Sys.Domain;

public class Contract
{
    public int Id { get; set; }
    public string RequestRefNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string TaxNo { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ContractStatus Status { get; set; }
    public int Stage { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ContractItem> Items { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<ApprovalLog> ApprovalLogs { get; set; } = new();
    public List<Violation> Violations { get; set; } = new();
}

public enum ContractStatus
{
    Talep,
    OnayBekliyor,
    Aktif,
    Uyari,
    Ihlal,
    Tamamlandi,
    Feshedildi
}