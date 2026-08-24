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

    // Kullanıcıya gösterilecek kural listesi. Şifre Değiştir ekranındaki kart bu
    // listeden besleniyor; kural değişirse ekran metnini ayrıca güncellemek
    // gerekmiyor (eskiden kart sabit metindi ve gerçek kuraldan kopabilirdi).
    public static readonly string[] Rules =
    {
        $"En az {MinLength} karakter",
        "En az bir harf",
        "En az bir rakam",
        "Mevcut şifreden farklı"
    };

    // Geçerliyse null, değilse kullanıcıya gösterilecek hata mesajı döner.
    //
    // Özel karakter zorunluluğu bilinçli olarak yok: insanları "Sifre123!" gibi
    // tahmin edilebilir kalıplara itiyor, gerçek faydası tartışmalı.
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Şifre boş olamaz.";

        if (password.Length < MinLength)
            return $"Şifre en az {MinLength} karakter olmalıdır.";

        if (!password.Any(char.IsLetter))
            return "Şifre en az bir harf içermelidir.";

        if (!password.Any(char.IsDigit))
            return "Şifre en az bir rakam içermelidir.";

        return null;
    }

    public static bool IsValid(string? password) => Validate(password) is null;

    // Ekrandaki canlı kural listesi için: hangi kuralın sağlandığı tek tek gerekiyor.
    // Validate() ilk hatada duruyor, burada hepsi ayrı ayrı değerlendiriliyor.
    public static bool HasMinLength(string? password) => (password?.Length ?? 0) >= MinLength;
    public static bool HasLetter(string? password) => password?.Any(char.IsLetter) == true;
    public static bool HasDigit(string? password) => password?.Any(char.IsDigit) == true;
}
