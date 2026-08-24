using Sys.Domain;

namespace Sys.UI;

// Rol adlarının kullanıcıya gösterilen karşılığı.
//
// Bu eşleme yalnızca UserRowViewModel içinde vardı; yeni kullanıcı formundaki rol
// açılır listesi ise enum değerlerini DOĞRUDAN bağlıyordu. Sonuç: listede
// "SYB Uzmanı" yazan rol, formda "SYB" olarak görünüyordu. Tek yere alındı.
public static class UserRoleHelper
{
    public static string ToLabel(UserRole role) => role switch
    {
        UserRole.Personel => "Personel",
        UserRole.SYB => "SYB Uzmanı",
        UserRole.Mudur => "Yönetim / Mali İşler",
        UserRole.Admin => "Sistem Yöneticisi",
        _ => role.ToString()
    };
}
