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

    // Son red HANGİ AŞAMADAN geldi? 0 talep incelemesi (SYB iadesi ya da talep
    // sahibinin geri çekmesi) · 1 SYB Son Kontrol · 2 Yönetim onayı.
    //
    // WasRejected "reddedildi mi" sorusuna cevap verir; bu alan "kim geri
    // gönderdi" sorusuna. İkisi ayrı sorular ve ekranda ayrı cümleler gerektiriyor:
    // Müdür'ün geri gönderdiği bir sözleşme ile kullanıcının kendi geri çektiği
    // talep aynı görünüyor (ikisi de Talep + reddedilmiş), ama kullanıcıya
    // söylenecek şey aynı değil.
    public int? LastRejectedStage { get; set; }
    public ContractStatus Status { get; set; }
    public int Stage { get; set; }

    // Bu sözleşme oluşturulurken Son Kontrol (Stage 1) atlandı mı?
    //
    // Atlanma kuralı sözleşme YARATILIRKEN işliyor (talebi açan ile sözleşmeyi
    // oluşturan aynı SYB mi). Ama sonucu daha sonra, Müdür reddettiğinde de
    // gerekiyor: atlanmış bir sözleşme reddedilince Son Kontrol'e "geri" dönemez —
    // orada hiç bulunmadı. Karar anında bunu yeniden hesaplamak mümkün değil,
    // çünkü o an işlemi yapan kişi Müdür. Bu yüzden kararın kendisi saklanıyor.
    public bool FinalCheckSkipped { get; set; }
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

    // Bu talep/sözleşme hangi sözleşmenin yenilenmesiyle doğdu?
    //
    // Sözleşmelerin çoğu yenileniyor ve bilgilerin neredeyse tamamı aynı kalıyor.
    // Yenileme, kaynak sözleşmenin verisiyle dolu bir talep formu açar; bu alan da
    // iki kaydı birbirine bağlar. Böylece bir sözleşmenin kaçıncı dönem olduğu ve
    // önceki döneme ait koşulların ne olduğu izlenebiliyor.
    //
    // null = yenileme değil, sıfırdan açılmış talep.
    public int? RenewedFromContractId { get; set; }

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
    Feshedildi,

    // Sözleşmeye hiç dönüşmeden SYB tarafından kapatılan talep. Enum değerleri
    // veritabanına int olarak yazıldığı için yeni değer MUTLAKA sonuna eklenir;
    // araya sokulursa mevcut kayıtların durumu kayar. Şema değişmediğinden bu
    // ekleme için migration gerekmez.
    Reddedildi
}