using Sys.UI.Localization;

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
    // Alan (readonly) değil ÖZELLİK: metinler o anki dilden çözülüyor. Sabit dizi
    // olsaydı uygulama açılışındaki dile kilitlenir, geçiş yapıldığında liste eski
    // dilde kalırdı.
    public static string[] Labels =>
    [
        Strings.T("Check.New1"),
        Strings.T("Check.New2"),
        Strings.T("Check.New3"),
        Strings.T("Check.New4"),
        Strings.T("Check.New5"),
    ];
}
