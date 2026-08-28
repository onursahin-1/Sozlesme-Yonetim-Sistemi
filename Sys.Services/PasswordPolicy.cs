namespace Sys.Services;

// Şifre kuralları. Tek yerde duruyor çünkü şifre üç ayrı yoldan belirlenebiliyor:
// yönetici yeni kullanıcı oluştururken, yönetici şifre sıfırlarken ve kullanıcı
// kendi şifresini değiştirirken. Kural eskiden yalnızca üçüncü yolda vardı —
// yönetici yolları hiçbir doğrulama yapmadan doğrudan hash'liyordu. Yani kullanıcı
// kendi şifresini "12345" yapamıyordu ama yönetici onun adına yapabiliyordu.
//
// ÖNEMLİ: Bu kurallar yalnızca ŞİFRE BELİRLENİRKEN çalışır, girişte değil.
// Mevcut kullanıcıların eski şifreleri geçerliliğini korur; kimse bu değişiklik
// yüzünden sisteme giremez duruma düşmez. Kural, bundan sonraki her şifre
// değişikliğinde uygulanır.
public static class PasswordPolicy
{
    public const int MinLength = 8;

    // Geçerliyse null, değilse ihlal edilen kuralın KODU döner.
    //
    // Eskiden metin dönüyordu ve o metin hem servis hem arayüz tarafından doğrudan
    // ekrana basılıyordu. Metin dile bağlı, kural değil.
    //
    // Özel karakter zorunluluğu bilinçli olarak yok: insanları "Sifre123!" gibi
    // tahmin edilebilir kalıplara itiyor, gerçek faydası tartışmalı.
    public static AppError? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return AppError.PasswordEmpty;

        if (password.Length < MinLength)
            return AppError.PasswordTooShort;

        if (!password.Any(char.IsLetter))
            return AppError.PasswordNeedsLetter;

        if (!password.Any(char.IsDigit))
            return AppError.PasswordNeedsDigit;

        return null;
    }

    public static bool IsValid(string? password) => Validate(password) is null;

    // Ekrandaki canlı kural listesi için: hangi kuralın sağlandığı tek tek gerekiyor.
    // Validate() ilk hatada duruyor, burada hepsi ayrı ayrı değerlendiriliyor.
    public static bool HasMinLength(string? password) => (password?.Length ?? 0) >= MinLength;
    public static bool HasLetter(string? password) => password?.Any(char.IsLetter) == true;
    public static bool HasDigit(string? password) => password?.Any(char.IsDigit) == true;
}
