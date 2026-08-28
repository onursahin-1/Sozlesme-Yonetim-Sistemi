using Sys.Services;

namespace Sys.UI.Localization;

// Servis hata kodlarını okunabilir metne çevirir.
//
// SERVİS METİN ÜRETMİYOR. İş kuralı "ne oldu"yu söylüyor (AppError), bunun hangi
// dilde nasıl yazılacağı arayüzün işi. Bu sınıf o sınırın arayüz tarafındaki ucu.
//
// Eşleme tablosu YOK: anahtar adı doğrudan kodun adından türetiliyor
// (AppError.NotAuthorized → "Err.NotAuthorized"). Elle tutulan bir tablo olsaydı
// yeni bir kod eklendiğinde tabloya eklemeyi unutmak mümkün olurdu; şimdi unutulan
// şey sözlükte eksik anahtar olarak, ekranda köşeli parantezle görünüyor.
public static class ErrorText
{
    public static string Of(AppError error, params object?[] args)
    {
        var key = "Err." + error;

        // Bazı hataların metninde parametre var. Şifre uzunluğu kuralı bunlardan
        // biri ama sayıyı servis göndermiyor — kural PasswordPolicy'de sabit, metin
        // burada kuruluyor.
        if (error == AppError.PasswordTooShort && args.Length == 0)
            return Strings.T(key, PasswordPolicy.MinLength);

        // "Bu sözleşme için {0} işlemi yapılamaz" — {0} da bir çeviri anahtarı.
        if (error == AppError.ContractNotLive && args.Length == 1 && args[0] is string opKey)
            return Strings.T(key, Strings.T(opKey));

        return args.Length == 0 ? Strings.T(key) : Strings.T(key, args);
    }

    // Uygulama açılışında bir kez bağlanıyor: bu andan sonra servis katmanının
    // fırlattığı AppException'ın Message'ı doğru dilde okunuyor ve arayüzdeki
    // mevcut "catch (Exception ex) → ex.Message" yolları olduğu gibi çalışıyor.
    public static void Install()
        => AppException.Describe = (error, args) => Of(error, args);
}
