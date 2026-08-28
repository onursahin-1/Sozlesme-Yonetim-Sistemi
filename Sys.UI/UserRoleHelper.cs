using Sys.Domain;
using Sys.UI.Localization;

namespace Sys.UI;

// Rol adlarının kullanıcıya gösterilen karşılığı.
//
// Bu eşleme yalnızca UserRowViewModel içinde vardı; yeni kullanıcı formundaki rol
// açılır listesi ise enum değerlerini DOĞRUDAN bağlıyordu. Sonuç: listede
// "SYB Uzmanı" yazan rol, formda "SYB" olarak görünüyordu. Tek yere alındı.
//
// Sonradan ikinci bir kopya daha çıktı: ShellViewModel.RoleLabel aynı eşlemeyi
// kendi içinde yapıyordu. Dil desteği eklenince o çevrildi, burası Türkçe kaldı —
// üst çubukta "Contracts Specialist" yazarken kullanıcı listesinde "SYB Uzmanı"
// görünüyordu. İkisi de artık aynı anahtarlardan besleniyor.
public static class UserRoleHelper
{
    public static string ToLabel(UserRole role) => role switch
    {
        UserRole.Personel => Strings.T("Role.Personel"),
        UserRole.SYB => Strings.T("Role.Syb"),
        UserRole.Mudur => Strings.T("Role.Mudur"),
        UserRole.Admin => Strings.T("Role.Admin"),
        _ => role.ToString()
    };
}
