namespace Sys.Domain;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Department { get; set; }

    // Güvenlik: hesap kilitleme
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }

    // Devre dışı bırakılmış hesaplar giriş yapamaz. Kullanıcıyı silmek yerine devre
    // dışı bırakmak, geçmişteki sözleşme/talep kayıtlarındaki "CreatedByUser"/
    // "ActingUser" referanslarının bozulmasını önler.
    public bool IsDisabled { get; set; }

    // Yönetici bu hesabın şifresini belirlediyse true olur; kullanıcı bir sonraki
    // girişinde şifresini değiştirmeden başka hiçbir ekrana geçemez.
    //
    // Neden gerekli: yöneticinin belirlediği şifreyi iki kişi biliyor. Kullanıcı onu
    // değiştirmezse, denetim kaydındaki "Ayşe Yılmaz — Son Kontrol onaylandı" satırı
    // Ayşe'yi mi yoksa yöneticiyi mi gösteriyor, sistem ayırt edemez. Sözleşme
    // onaylayan bir uygulamada denetim kaydının tüm değeri bu ayrımı yapabilmesinden
    // geliyor.
    //
    // Mevcut kullanıcılar için false: bu değişiklik kimseyi şifre değiştirmeye
    // zorlamıyor, yalnızca bundan sonra yönetici tarafından belirlenen şifreler
    // için geçerli.
    public bool MustChangePassword { get; set; }

    // Şifrenin en son ne zaman değiştiği. Zorlama yok, yalnızca bilgi: denetimde
    // "bu hesabın şifresi ne kadardır değişmemiş" sorusuna cevap verir.
    // Mevcut kayıtlarda null — geçmişte ne zaman değiştiği bilinmiyor ve uydurmak
    // yanlış bilgi üretmek olurdu.
    public DateTime? PasswordChangedAt { get; set; }
}

// Admin en sona eklendi: EF Core enum değerlerini tamsayı olarak sakladığı için,
// mevcut kullanıcıların rol değerlerinin bozulmaması adına yeni değerler her zaman
// sona eklenmeli, araya sıkıştırılmamalı.
public enum UserRole
{
    Personel,
    SYB,
    Mudur,
    Admin
}