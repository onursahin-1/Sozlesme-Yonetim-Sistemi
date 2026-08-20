using System;
using System.Runtime.InteropServices;

namespace Sys.UI;

// Caps Lock durumunu okur.
//
// Avalonia'da kilit tuşlarının (Caps Lock, Num Lock) durumunu veren bir API yok:
// KeyModifiers yalnızca Shift/Ctrl/Alt gibi basılı tutulan tuşları bildiriyor.
// Bu yüzden Windows'un GetKeyState fonksiyonu kullanılıyor. Uygulama zaten
// Windows'a kurulu SQL Server ile çalıştığı için bu bir kısıt oluşturmuyor;
// yine de başka bir işletim sisteminde çalıştırılırsa uyarı sessizce gizleniyor.
internal static class KeyboardState
{
    private const int VkCapital = 0x14;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    public static bool IsCapsLockOn()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            // Dönen değerin en düşük biti, tuşun "açık" (toggled) olup olmadığını verir.
            // Yüksek bit ise tuşun o an basılı tutulup tutulmadığını gösterir; bizi
            // ilgilendiren açık/kapalı durumu olduğu için düşük bite bakılıyor.
            return (GetKeyState(VkCapital) & 1) == 1;
        }
        catch
        {
            return false;
        }
    }
}
