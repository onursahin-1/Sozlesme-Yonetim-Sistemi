using System;

namespace Sys.Domain;

public class ContractRevision
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal PreviousTotalAmount { get; set; }
    public DateTime? PreviousEndDate { get; set; }
    public string PreviousDescription { get; set; } = string.Empty;

    // "Firma Bilgisi Güncelleme" ve "Ödeme Koşulları Değişikliği" türündeki
    // düzenlemelerde eski değerleri saklamak için eklendi — bir düzenleme
    // reddedilirse bu alanlar üzerinden sözleşme eski haline döndürülür.
    public string PreviousCompanyName { get; set; } = string.Empty;
    public string PreviousTaxNo { get; set; } = string.Empty;
    public string? PreviousPaymentPeriod { get; set; }

    public int ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }

    // Düzenleme talebinin SONUCU.
    //
    // Bu alanlar olmadan geçmiş yanlış bilgi veriyordu: reddedilmiş bir bedel
    // değişikliği de "Revizyon Geçmişi"nde gerçekleşmiş gibi duruyordu. Kayıt hem
    // talebi hem sonucunu temsil ediyor.
    //   null  → karar bekliyor (ya da bu alanlar eklenmeden önce oluşmuş eski kayıt)
    //   true  → onaylandı, değişiklik yürürlüğe girdi
    //   false → reddedildi, sözleşme eski değerlerine döndürüldü
    public bool? IsApproved { get; set; }

    public DateTime? ResolvedAt { get; set; }
}