using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace Sys.Domain;

public class Contract
{
    public int Id { get; set; }
    public string RequestRefNo { get; set; } = string.Empty;
    public string? ContractNo { get; set; }
    public string? PaymentPeriod { get; set; }
    public string? SapCariKodu { get; set; }
    public string? CompanyType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string TaxNo { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool WasRejected { get; set; }
    public string? LastRejectionNote { get; set; }
    public DateTime? LastRejectedAt { get; set; }
    public ContractStatus Status { get; set; }
    public int Stage { get; set; }
    public bool PendingTermination { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal TotalAmount { get; set; }

    // Sözleşmenin para birimi (ISO kodu: TRY / EUR / USD). Tutarlar tek bir para
    // biriminde tutulur; kur dönüşümü yapılmaz. Eski kayıtlar için varsayılan TRY.
    public string Currency { get; set; } = "TRY";
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<ContractItem> Items { get; set; } = new();
    public List<Attachment> Attachments { get; set; } = new();
    public List<ApprovalLog> ApprovalLogs { get; set; } = new();
    public List<Violation> Violations { get; set; } = new();
    public List<ContractRevision> Revisions { get; set; } = new();
    public List<ContractTermination> Terminations { get; set; } = new();
    public bool PendingEdit { get; set; }
    public ContractStatus? PreviousStatusBeforeEdit { get; set; }

    // Fesih talebi reddedildiğinde sözleşmenin fesih öncesi durumuna (Aktif veya Uyarı)
    // geri dönebilmesi için, talep anındaki durum burada saklanır.
    public ContractStatus? PreviousStatusBeforeTermination { get; set; }

    // SQL Server tarafından her güncellemede otomatik artırılan concurrency token.
    // İki kullanıcı aynı sözleşmeyi aynı anda işleme alırsa (örn. iki Müdür aynı
    // sözleşmeyi onaylarsa), ikinci kaydetme işlemi bu alan sayesinde reddedilir —
    // aksi halde birinin işlemi fark edilmeden diğerinin üzerine yazılabilirdi.
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
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