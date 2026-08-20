namespace Sys.Domain;

// Giriş ekranındaki "Şifremi unuttum" akışında oluşturulan sıfırlama talebi.
//
// E-posta altyapısı olmadığı için otomatik sıfırlama bağlantısı gönderilemiyor.
// Bunun yerine talep kayda geçiyor, Admin Kullanıcı Yönetimi ekranında görüyor ve
// şifreyi sıfırlayıp kullanıcıya iletiyor. Böylece talepler unutulmuyor ve kimin
// ne zaman istediği izlenebiliyor.
public class PasswordResetRequest
{
    public int Id { get; set; }

    // Kullanıcı adı serbest metin olarak da saklanır: girilen ad sistemde yoksa
    // (yazım hatası olabilir) Admin bunu görüp kullanıcıya doğrusunu söyleyebilsin.
    public string Username { get; set; } = string.Empty;

    // Eşleşen bir kullanıcı bulunduysa kimliği; bulunamadıysa null.
    public int? UserId { get; set; }

    public DateTime RequestedAt { get; set; }

    // Admin talebi işleyip şifreyi sıfırladığında veya talebi kapattığında doldurulur.
    public bool IsHandled { get; set; }
    public DateTime? HandledAt { get; set; }
    public int? HandledByUserId { get; set; }
}
