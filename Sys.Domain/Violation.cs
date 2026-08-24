using System;

namespace Sys.Domain;

public class Violation
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public DateTime ViolationDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ReportedByUserId { get; set; }
    public DateTime ReportedAt { get; set; }

    // İhlalin GİDERİLDİĞİ bilgisi.
    //
    // Bu alanlar olmadan ihlal kalıcı bir damgaydı: sözleşme "İhlal Mevcut" durumuna
    // girdikten sonra tek çıkışı fesih ya da süresinin dolmasıydı. Oysa ihlal
    // giderilebilir bir durumdur (eksik iş tamamlanır, gecikme telafi edilir).
    // Sözleşmenin açık ihlali kalmadığında durumu normale döner.
    //
    // ResolvedAt null ise ihlal açıktır.
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }

    // Neyin nasıl giderildiği; denetim açısından bildirimin kendisi kadar önemli.
    public string? ResolutionNote { get; set; }

    public bool IsResolved => ResolvedAt.HasValue;
}