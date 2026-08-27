namespace Sys.UI;

// Yeni sözleşme için veri giriş kontrol listesi.
//
// Bu maddeler İKİ yerde gösteriliyor ve ikisinde de aynı olmalı:
//   - Sözleşme Yarat sihirbazının son adımında (talebi de aynı SYB açtıysa;
//     o durumda Son Kontrol atlandığı için liste başka hiçbir yerde sorulmuyor)
//   - Son Kontrol ekranında (talep Personel'den geldiyse)
//
// Tek kaynakta tutuluyor çünkü aynı liste iki yerde ayrı yazılsaydı zamanla
// ayrışırdı — bu turlarda birkaç kez rastladığımız hata sınıfı.
//
// Maddeler VERİ GİRİŞİNİ doğruluyor (SAP cari kodu, bedel kalemleri, tarihler),
// kararı değil. Bu yüzden asıl yerleri veri girişinin sonu.
public static class WizardChecklist
{
    public static readonly string[] Labels =
    {
        "Kapsam, talebin konusuyla örtüşüyor",
        "Bedel kalemleri ve toplam tutar doğru",
        "Firma bilgileri doğru (Vergi No, SAP Cari Kodu)",
        "Başlangıç/Bitiş tarihleri ve ödeme periyodu doğru",
        "Sözleşme dosyası yüklendi; ek ve teminat belgeleri tam",
    };
}
