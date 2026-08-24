using System;

namespace Sys.Domain;

public class ContractTermination
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string TerminationType { get; set; } = string.Empty;
    public DateTime TerminationDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? CompensationAmount { get; set; }
    public string CompensationDirection { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }

    // Fesih talebinin SONUCU. ContractRevision.IsApproved ile aynı anlam:
    // reddedilmiş bir fesih talebi de "Fesih Geçmişi"nde sözleşme feshedilmiş gibi
    // görünüyordu.
    //   null  → karar bekliyor (ya da bu alanlar eklenmeden önce oluşmuş eski kayıt)
    //   true  → onaylandı, sözleşme feshedildi
    //   false → reddedildi, sözleşme fesih öncesi durumuna döndü
    public bool? IsApproved { get; set; }

    public DateTime? ResolvedAt { get; set; }
}